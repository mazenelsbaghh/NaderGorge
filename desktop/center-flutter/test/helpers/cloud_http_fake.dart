import 'dart:async';
import 'dart:convert';
import 'dart:io';

/// Only the HTTP boundary is simulated; controllers use their real file storage.
class ScriptedCloudHttp {
  ScriptedCloudHttp(this.reply);
  final FutureOr<CloudHttpReply> Function(CloudHttpRequest) reply;
  final requests = <CloudHttpRequest>[];

  HttpClient createClient() => _CloudHttpClient(this);
}

class CloudHttpReply {
  CloudHttpReply(
    this.status,
    this.bytes, {
    int? contentLength,
    this.compressionState = HttpClientResponseCompressionState.notCompressed,
  }) : contentLength = contentLength ?? bytes.length;
  CloudHttpReply.json(int status, Object body)
    : this(status, utf8.encode(jsonEncode(body)));
  final int status;
  final List<int> bytes;
  final int contentLength;
  final HttpClientResponseCompressionState compressionState;
}

class CloudHttpRequest implements HttpClientRequest {
  CloudHttpRequest(this._script, this.method, this.uri);
  final ScriptedCloudHttp _script;
  @override
  final String method;
  @override
  final Uri uri;
  @override
  final headers = _CloudHttpHeaders();
  @override
  bool followRedirects = true;
  @override
  int maxRedirects = 5;
  @override
  int contentLength = -1;
  final bytes = <int>[];
  String get body => utf8.decode(bytes);
  @override
  void add(List<int> chunk) => bytes.addAll(chunk);
  @override
  Future<HttpClientResponse> close() async {
    _script.requests.add(this);
    return _CloudHttpResponse(await _script.reply(this));
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnsupportedError(
    'Unexpected HTTP request member ${invocation.memberName}',
  );
}

class _CloudHttpClient implements HttpClient {
  _CloudHttpClient(this.script);
  final ScriptedCloudHttp script;
  @override
  Duration? connectionTimeout;
  @override
  bool autoUncompress = true;
  @override
  Future<HttpClientRequest> postUrl(Uri url) async =>
      CloudHttpRequest(script, 'POST', url);
  @override
  Future<HttpClientRequest> getUrl(Uri url) async =>
      CloudHttpRequest(script, 'GET', url);
  @override
  void close({bool force = false}) {}
  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnsupportedError(
    'Unexpected HTTP client member ${invocation.memberName}',
  );
}

class _CloudHttpHeaders implements HttpHeaders {
  final _headers = <String, List<String>>{};
  @override
  ContentType? contentType;
  @override
  void set(String name, Object value, {bool preserveHeaderCase = false}) =>
      _headers[name.toLowerCase()] = [value.toString()];
  @override
  String? value(String name) => _headers[name.toLowerCase()]?.join(',');
  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnsupportedError(
    'Unexpected HTTP header member ${invocation.memberName}',
  );
}

class _CloudHttpResponse extends Stream<List<int>>
    implements HttpClientResponse {
  _CloudHttpResponse(this.reply);
  final CloudHttpReply reply;
  @override
  int get statusCode => reply.status;
  @override
  HttpClientResponseCompressionState get compressionState =>
      reply.compressionState;
  @override
  int get contentLength => reply.contentLength;
  @override
  StreamSubscription<List<int>> listen(
    void Function(List<int>)? onData, {
    Function? onError,
    void Function()? onDone,
    bool? cancelOnError,
  }) => Stream<List<int>>.fromIterable([reply.bytes]).listen(
    onData,
    onError: onError,
    onDone: onDone,
    cancelOnError: cancelOnError,
  );
  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnsupportedError(
    'Unexpected HTTP response member ${invocation.memberName}',
  );
}
