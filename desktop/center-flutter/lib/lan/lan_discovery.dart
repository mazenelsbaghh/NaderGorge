import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_transport.dart';
import '../shared/problem_reporting.dart';

class LanDiscovery {
  static const port = 43874;

  static Future<List<LanEndpoint>> search({
    Duration timeout = const Duration(seconds: 2),
    int discoveryPort = port,
    List<InternetAddress>? targets,
    Future<RawDatagramSocket> Function()? bindSocket,
    Future<List<InternetAddress>> Function()? listSourceAddresses,
    Future<RawDatagramSocket> Function(InternetAddress)? bindSourceSocket,
  }) async {
    final watch = Stopwatch()..start();
    var adapterCount = 0, hostCount = 0;
    Object? failure;
    try {
      // Explicit destinations and socket injection retain the single-socket API.
      if (listSourceAddresses == null &&
          bindSourceSocket == null &&
          (targets != null || bindSocket != null)) {
        adapterCount = 1;
        final endpoints = _deduplicate(
          await _searchSocket(
            timeout: timeout,
            discoveryPort: discoveryPort,
            targets: targets,
            bindSocket: bindSocket,
          ),
        );
        hostCount = endpoints.length;
        return endpoints;
      }
      final clock = Stopwatch()..start();
      final List<InternetAddress> sources;
      try {
        sources = await (listSourceAddresses?.call() ?? _localIPv4()).timeout(
          timeout,
        );
      } on SocketException catch (error, stack) {
        throw CenterException(
          'تعذر فحص اتصالات الشبكة.',
          cause: error,
          stackTrace: stack,
        );
      } on TimeoutException catch (error, stack) {
        throw CenterException(
          'انتهت مهلة فحص اتصالات الشبكة.',
          cause: error,
          stackTrace: stack,
        );
      }
      final unique = <String, InternetAddress>{
        for (final address in sources)
          if (address.type == InternetAddressType.IPv4 &&
              address.address != '0.0.0.0')
            address.address: address,
      }.values.take(8).toList();
      adapterCount = unique.length;
      if (unique.isEmpty) {
        throw const CenterException('لا يوجد اتصال شبكة محلي متاح للبحث.');
      }
      final discoveryTime =
          timeout - clock.elapsed - const Duration(milliseconds: 250);
      final errors = <Object>[];
      StackTrace? failureStack;
      final batches = await Future.wait([
        for (final source in unique)
          () async {
            try {
              final endpoints = await _searchSocket(
                timeout: discoveryTime > Duration.zero
                    ? discoveryTime
                    : const Duration(milliseconds: 1),
                discoveryPort: discoveryPort,
                targets:
                    targets ??
                    [
                      if (source.isLoopback)
                        InternetAddress.loopbackIPv4
                      else
                        InternetAddress('255.255.255.255'),
                    ],
                bindSocket: () =>
                    bindSourceSocket?.call(source) ??
                    RawDatagramSocket.bind(source, 0),
              );
              return [
                for (final endpoint in endpoints)
                  _DiscoveryCandidate(endpoint, source),
              ];
            } on CenterException catch (error, stack) {
              reportLan('lan.discovery', 'adapter', error: error);
              errors.add(error.cause ?? error);
              failureStack ??= stack;
              return <_DiscoveryCandidate>[];
            }
          }(),
      ]);
      final candidates = batches.expand((batch) => batch).take(64).toList();
      if (candidates.isEmpty && errors.isNotEmpty) {
        throw CenterException(
          'تعذر البحث عن أجهزة السنتر على الشبكة.',
          cause: List<Object>.unmodifiable(errors),
          stackTrace: failureStack,
        );
      }
      final remaining = timeout - clock.elapsed;
      final reachable = await Future.wait([
        for (final candidate in candidates) _reachable(candidate, remaining),
      ]);
      final endpoints = _deduplicate([
        for (final loopback in [false, true])
          for (var index = 0; index < candidates.length; index++)
            if (reachable[index] &&
                InternetAddress(
                      candidates[index].endpoint.address,
                    ).isLoopback ==
                    loopback)
              candidates[index].endpoint,
        for (var index = 0; index < candidates.length; index++)
          if (!reachable[index]) candidates[index].endpoint,
      ], preferFirst: true);
      hostCount = endpoints.length;
      return endpoints;
    } catch (error) {
      failure = error;
      rethrow;
    } finally {
      reportLan(
        'lan.discovery',
        'discovery',
        error: failure,
        metrics: {
          'durationMs': watch.elapsedMilliseconds,
          'port': discoveryPort,
          'adapters': adapterCount,
          'hosts': hostCount,
        },
      );
    }
  }

  static Future<List<InternetAddress>> _localIPv4() async => [
    for (final network in await NetworkInterface.list(
      type: InternetAddressType.IPv4,
      includeLoopback: false,
    ))
      ...network.addresses,
    InternetAddress.loopbackIPv4,
  ];

  static Future<bool> _reachable(
    _DiscoveryCandidate candidate,
    Duration remaining,
  ) async {
    if (remaining <= Duration.zero) return false;
    Socket? connection;
    try {
      connection = await Socket.connect(
        candidate.endpoint.address,
        candidate.endpoint.port,
        sourceAddress: candidate.source,
        timeout: remaining,
      );
      return true;
    } on SocketException {
      return false;
    } on TimeoutException {
      return false;
    } finally {
      connection?.destroy();
    }
  }

  static List<LanEndpoint> _deduplicate(
    List<LanEndpoint> candidates, {
    bool preferFirst = false,
  }) {
    final endpoints = <String, LanEndpoint>{};
    for (final endpoint in candidates) {
      final previous = endpoints[endpoint.hostId];
      if (previous == null ||
          (!preferFirst && InternetAddress(previous.address).isLoopback)) {
        endpoints[endpoint.hostId] = endpoint;
      }
    }
    return List.unmodifiable(endpoints.values);
  }

  static Future<List<LanEndpoint>> _searchSocket({
    required Duration timeout,
    required int discoveryPort,
    List<InternetAddress>? targets,
    Future<RawDatagramSocket> Function()? bindSocket,
  }) async {
    final deadline = Stopwatch()..start();
    RawDatagramSocket? socket;
    StreamSubscription<RawSocketEvent>? subscription;
    final endpoints = <String, LanEndpoint>{};
    Object? sendFailure;
    StackTrace? sendStack;
    var socketFailed = false;
    try {
      final binding =
          bindSocket?.call() ??
          RawDatagramSocket.bind(InternetAddress.anyIPv4, 0);
      try {
        socket = await binding.timeout(timeout);
      } on TimeoutException {
        // Closing a socket that arrives late avoids leaking it after the deadline.
        unawaited(
          binding.then<void>(
            (lateSocket) => lateSocket.close(),
            onError: (Object error) {},
          ),
        );
        throw const SocketException('Discovery socket binding timed out');
      }
      socket.broadcastEnabled = true;
      subscription = socket.listen(
        (event) {
          if (event != RawSocketEvent.read) return;
          Datagram? packet;
          while ((packet = socket!.receive()) != null) {
            if (packet!.data.length > 4096) continue;
            try {
              final json = jsonDecode(utf8.decode(packet.data));
              if (json is! Map<String, dynamic> ||
                  json['kind'] != 'massar-host' ||
                  json['protocol'] != 1) {
                continue;
              }
              final endpoint = LanEndpoint.fromJson({
                ...json,
                'address': packet.address.address,
              });
              final key =
                  '${endpoint.hostId}|${endpoint.address}|${endpoint.port}';
              if (endpoints.length < 64 || endpoints.containsKey(key)) {
                endpoints[key] = endpoint;
              }
            } on FormatException {
              // Ignore unrelated or malformed UDP packets, never trust their text.
            } on TypeError {
              // Field types are untrusted network input.
            }
          }
        },
        onError: (Object error, StackTrace stackTrace) {
          socketFailed = true;
          sendFailure = error;
          sendStack = stackTrace;
        },
      );
      final query = utf8.encode(
        jsonEncode({'kind': 'massar-discover', 'protocol': 1}),
      );
      await Future.wait([
        for (final destination
            in targets ??
                [
                  InternetAddress.loopbackIPv4,
                  InternetAddress('255.255.255.255'),
                ])
          () async {
            try {
              // A zero return means the OS send buffer is temporarily full.
              // Retry only the read-only discovery query, within its deadline.
              while (!socketFailed &&
                  socket!.send(query, destination, discoveryPort) == 0) {
                final remaining = timeout - deadline.elapsed;
                if (remaining <= Duration.zero) {
                  throw const SocketException('Discovery send timed out');
                }
                await Future<void>.delayed(
                  remaining < const Duration(milliseconds: 25)
                      ? remaining
                      : const Duration(milliseconds: 25),
                );
              }
            } on SocketException catch (error, stackTrace) {
              sendFailure ??= error;
              sendStack ??= stackTrace;
            }
          }(),
      ]);
      final remaining = timeout - deadline.elapsed;
      if (remaining > Duration.zero) {
        await Future<void>.delayed(remaining);
      }
      if (endpoints.isEmpty && sendFailure != null) {
        throw CenterException(
          'تعذر البحث عن أجهزة السنتر على الشبكة.',
          cause: sendFailure,
          stackTrace: sendStack,
        );
      }
      return List.unmodifiable(endpoints.values);
    } on SocketException catch (error, stackTrace) {
      throw CenterException(
        'تعذر البحث عن أجهزة السنتر على الشبكة.',
        cause: error,
        stackTrace: stackTrace,
      );
    } finally {
      await subscription?.cancel();
      socket?.close();
    }
  }
}

class _DiscoveryCandidate {
  const _DiscoveryCandidate(this.endpoint, this.source);
  final LanEndpoint endpoint;
  final InternetAddress source;
}
