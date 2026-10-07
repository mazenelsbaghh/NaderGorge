import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_transport.dart';

class LanDiscovery {
  static const port = 43874;

  static Future<List<LanEndpoint>> search({
    Duration timeout = const Duration(seconds: 2),
    int discoveryPort = port,
    List<InternetAddress>? targets,
    Future<RawDatagramSocket> Function()? bindSocket,
  }) async {
    RawDatagramSocket? socket;
    StreamSubscription<RawSocketEvent>? subscription;
    final endpoints = <String, LanEndpoint>{};
    Object? sendFailure;
    StackTrace? sendStack;
    var socketFailed = false;
    try {
      socket =
          await (bindSocket?.call() ??
              RawDatagramSocket.bind(InternetAddress.anyIPv4, 0));
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
              final previous = endpoints[endpoint.hostId];
              if (previous == null ||
                  InternetAddress(previous.address).isLoopback) {
                endpoints[endpoint.hostId] = endpoint;
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
      final deadline = Stopwatch()..start();
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
