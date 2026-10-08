import 'dart:collection';

class _SharedRows<T> {
  _SharedRows(this.rows);
  final List<T> rows;
  bool shared = false;
  Object identity = Object();
}

/// Both branches detach on a write, including mutations to the older branch.
class CopyOnWriteList<T> extends ListBase<T> {
  CopyOnWriteList(List<T> rows) : _storage = _SharedRows(List.of(rows));
  CopyOnWriteList._(this._storage);
  _SharedRows<T> _storage;

  CopyOnWriteList<T> fork() {
    _storage.shared = true;
    return CopyOnWriteList._(_storage);
  }

  Object get identity => _storage.identity;

  List<T> get _writable {
    if (_storage.shared) _storage = _SharedRows(List.of(_storage.rows));
    _storage.identity = Object();
    return _storage.rows;
  }

  @override
  int get length => _storage.rows.length;
  @override
  set length(int length) => _writable.length = length;
  @override
  T operator [](int index) => _storage.rows[index];
  @override
  void operator []=(int index, T row) => _writable[index] = row;
  @override
  void add(T element) => _writable.add(element);
  @override
  void addAll(Iterable<T> iterable) => _writable.addAll(iterable);
  @override
  void insert(int index, T element) => _writable.insert(index, element);
  @override
  void insertAll(int index, Iterable<T> iterable) =>
      _writable.insertAll(index, iterable);
  @override
  T removeAt(int index) => _writable.removeAt(index);
  @override
  void removeRange(int start, int end) => _writable.removeRange(start, end);
  @override
  void clear() => _writable.clear();
}

List<T> forkRows<T>(List<T> rows) =>
    rows is CopyOnWriteList<T> ? rows.fork() : CopyOnWriteList(rows);

Object rowsIdentity<T>(List<T> rows) =>
    rows is CopyOnWriteList<T> ? rows.identity : rows;
