//#region \0rolldown/runtime.js
var e = Object.create, t = Object.defineProperty, n = Object.getOwnPropertyDescriptor, r = Object.getOwnPropertyNames, i = Object.getPrototypeOf, a = Object.prototype.hasOwnProperty, o = (e, t) => () => (t || (e((t = { exports: {} }).exports, t), e = null), t.exports), s = (e, i, o, s) => {
	if (i && typeof i == "object" || typeof i == "function") for (var c = r(i), l = 0, u = c.length, d; l < u; l++) d = c[l], !a.call(e, d) && d !== o && t(e, d, {
		get: ((e) => i[e]).bind(null, d),
		enumerable: !(s = n(i, d)) || s.enumerable
	});
	return e;
}, c = (n, r, o) => (o = n == null ? {} : e(i(n)), s(r || !n || !n.__esModule || !a.call(n, "default") ? t(o, "default", {
	value: n,
	enumerable: !0
}) : o, n)), l = /* @__PURE__ */ o(((e) => {
	var t = Symbol.for("react.transitional.element"), n = Symbol.for("react.portal"), r = Symbol.for("react.fragment"), i = Symbol.for("react.strict_mode"), a = Symbol.for("react.profiler"), o = Symbol.for("react.consumer"), s = Symbol.for("react.context"), c = Symbol.for("react.forward_ref"), l = Symbol.for("react.suspense"), u = Symbol.for("react.memo"), d = Symbol.for("react.lazy"), f = Symbol.for("react.activity"), p = Symbol.for("react.view_transition"), m = Symbol.iterator;
	function h(e) {
		return typeof e != "object" || !e ? null : (e = m && e[m] || e["@@iterator"], typeof e == "function" ? e : null);
	}
	var g = {
		isMounted: function() {
			return !1;
		},
		enqueueForceUpdate: function() {},
		enqueueReplaceState: function() {},
		enqueueSetState: function() {}
	}, _ = Object.assign, v = {};
	function y(e, t, n) {
		this.props = e, this.context = t, this.refs = v, this.updater = n || g;
	}
	y.prototype.isReactComponent = {}, y.prototype.setState = function(e, t) {
		if (typeof e != "object" && typeof e != "function" && e != null) throw Error("takes an object of state variables to update or a function which returns an object of state variables.");
		this.updater.enqueueSetState(this, e, t, "setState");
	}, y.prototype.forceUpdate = function(e) {
		this.updater.enqueueForceUpdate(this, e, "forceUpdate");
	};
	function b() {}
	b.prototype = y.prototype;
	function x(e, t, n) {
		this.props = e, this.context = t, this.refs = v, this.updater = n || g;
	}
	var S = x.prototype = new b();
	S.constructor = x, _(S, y.prototype), S.isPureReactComponent = !0;
	var C = Array.isArray;
	function w() {}
	var T = {
		H: null,
		A: null,
		T: null,
		S: null
	}, E = Object.prototype.hasOwnProperty;
	function D(e, n, r) {
		var i = r.ref;
		return {
			$$typeof: t,
			type: e,
			key: n,
			ref: i === void 0 ? null : i,
			props: r
		};
	}
	function O(e, t) {
		return D(e.type, t, e.props);
	}
	function k(e) {
		return typeof e == "object" && !!e && e.$$typeof === t;
	}
	function ee(e) {
		var t = {
			"=": "=0",
			":": "=2"
		};
		return "$" + e.replace(/[=:]/g, function(e) {
			return t[e];
		});
	}
	var A = /\/+/g;
	function te(e, t) {
		return typeof e == "object" && e && e.key != null ? ee("" + e.key) : t.toString(36);
	}
	function ne(e) {
		switch (e.status) {
			case "fulfilled": return e.value;
			case "rejected": throw e.reason;
			default: switch (typeof e.status == "string" ? e.then(w, w) : (e.status = "pending", e.then(function(t) {
				e.status === "pending" && (e.status = "fulfilled", e.value = t);
			}, function(t) {
				e.status === "pending" && (e.status = "rejected", e.reason = t);
			})), e.status) {
				case "fulfilled": return e.value;
				case "rejected": throw e.reason;
			}
		}
		throw e;
	}
	function re(e, r, i, a, o) {
		var s = typeof e;
		(s === "undefined" || s === "boolean") && (e = null);
		var c = !1;
		if (e === null) c = !0;
		else switch (s) {
			case "bigint":
			case "string":
			case "number":
				c = !0;
				break;
			case "object": switch (e.$$typeof) {
				case t:
				case n:
					c = !0;
					break;
				case d: return c = e._init, re(c(e._payload), r, i, a, o);
			}
		}
		if (c) return o = o(e), c = a === "" ? "." + te(e, 0) : a, C(o) ? (i = "", c != null && (i = c.replace(A, "$&/") + "/"), re(o, r, i, "", function(e) {
			return e;
		})) : o != null && (k(o) && (o = O(o, i + (o.key == null || e && e.key === o.key ? "" : ("" + o.key).replace(A, "$&/") + "/") + c)), r.push(o)), 1;
		c = 0;
		var l = a === "" ? "." : a + ":";
		if (C(e)) for (var u = 0; u < e.length; u++) a = e[u], s = l + te(a, u), c += re(a, r, i, s, o);
		else if (u = h(e), typeof u == "function") for (e = u.call(e), u = 0; !(a = e.next()).done;) a = a.value, s = l + te(a, u++), c += re(a, r, i, s, o);
		else if (s === "object") {
			if (typeof e.then == "function") return re(ne(e), r, i, a, o);
			throw r = String(e), Error("Objects are not valid as a React child (found: " + (r === "[object Object]" ? "object with keys {" + Object.keys(e).join(", ") + "}" : r) + "). If you meant to render a collection of children, use an array instead.");
		}
		return c;
	}
	function j(e, t, n) {
		if (e == null) return e;
		var r = [], i = 0;
		return re(e, r, "", "", function(e) {
			return t.call(n, e, i++);
		}), r;
	}
	function M(e) {
		if (e._status === -1) {
			var t = e._result, n = t();
			n.then(function(t) {
				(e._status === 0 || e._status === -1) && (e._status = 1, e._result = t, n.status === void 0 && (n.status = "fulfilled", n.value = t));
			}, function(t) {
				(e._status === 0 || e._status === -1) && (e._status = 2, e._result = t, n.status === void 0 && (n.status = "rejected", n.reason = t));
			}), e._status === -1 && (e._status = 0, e._result = n);
		}
		if (e._status === 1) return e._result.default;
		throw e._result;
	}
	var ie = typeof reportError == "function" ? reportError : function(e) {
		if (typeof window == "object" && typeof window.ErrorEvent == "function") {
			var t = new window.ErrorEvent("error", {
				bubbles: !0,
				cancelable: !0,
				message: typeof e == "object" && e && typeof e.message == "string" ? String(e.message) : String(e),
				error: e
			});
			if (!window.dispatchEvent(t)) return;
		} else if (typeof process == "object" && typeof process.emit == "function") {
			process.emit("uncaughtException", e);
			return;
		}
		console.error(e);
	};
	function ae(e) {
		var t = T.T, n = {};
		n.types = t === null ? null : t.types, T.T = n;
		try {
			var r = e(), i = T.S;
			i !== null && i(n, r), typeof r == "object" && r && typeof r.then == "function" && r.then(w, ie);
		} catch (e) {
			ie(e);
		} finally {
			t !== null && n.types !== null && (t.types = n.types), T.T = t;
		}
	}
	function oe(e) {
		var t = T.T;
		if (t !== null) {
			var n = t.types;
			n === null ? t.types = [e] : n.indexOf(e) === -1 && n.push(e);
		} else ae(oe.bind(null, e));
	}
	var se = {
		map: j,
		forEach: function(e, t, n) {
			j(e, function() {
				t.apply(this, arguments);
			}, n);
		},
		count: function(e) {
			var t = 0;
			return j(e, function() {
				t++;
			}), t;
		},
		toArray: function(e) {
			return j(e, function(e) {
				return e;
			}) || [];
		},
		only: function(e) {
			if (!k(e)) throw Error("React.Children.only expected to receive a single React element child.");
			return e;
		}
	};
	e.Activity = f, e.Children = se, e.Component = y, e.Fragment = r, e.Profiler = a, e.PureComponent = x, e.StrictMode = i, e.Suspense = l, e.ViewTransition = p, e.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE = T, e.__COMPILER_RUNTIME = {
		__proto__: null,
		c: function(e) {
			return T.H.useMemoCache(e);
		}
	}, e.addTransitionType = oe, e.cache = function(e) {
		return function() {
			return e.apply(null, arguments);
		};
	}, e.cacheSignal = function() {
		return null;
	}, e.cloneElement = function(e, t, n) {
		if (e == null) throw Error("The argument must be a React element, but you passed " + e + ".");
		var r = _({}, e.props), i = e.key;
		if (t != null) for (a in t.key !== void 0 && (i = "" + t.key), t) !E.call(t, a) || a === "key" || a === "__self" || a === "__source" || a === "ref" && t.ref === void 0 || (r[a] = t[a]);
		var a = arguments.length - 2;
		if (a === 1) r.children = n;
		else if (1 < a) {
			for (var o = Array(a), s = 0; s < a; s++) o[s] = arguments[s + 2];
			r.children = o;
		}
		return D(e.type, i, r);
	}, e.createContext = function(e) {
		return e = {
			$$typeof: s,
			_currentValue: e,
			_currentValue2: e,
			_threadCount: 0,
			Provider: null,
			Consumer: null
		}, e.Provider = e, e.Consumer = {
			$$typeof: o,
			_context: e
		}, e;
	}, e.createElement = function(e, t, n) {
		var r, i = {}, a = null;
		if (t != null) for (r in t.key !== void 0 && (a = "" + t.key), t) E.call(t, r) && r !== "key" && r !== "__self" && r !== "__source" && (i[r] = t[r]);
		var o = arguments.length - 2;
		if (o === 1) i.children = n;
		else if (1 < o) {
			for (var s = Array(o), c = 0; c < o; c++) s[c] = arguments[c + 2];
			i.children = s;
		}
		if (e && e.defaultProps) for (r in o = e.defaultProps, o) i[r] === void 0 && (i[r] = o[r]);
		return D(e, a, i);
	}, e.createRef = function() {
		return { current: null };
	}, e.forwardRef = function(e) {
		return {
			$$typeof: c,
			render: e
		};
	}, e.isValidElement = k, e.lazy = function(e) {
		return {
			$$typeof: d,
			_payload: {
				_status: -1,
				_result: e
			},
			_init: M
		};
	}, e.memo = function(e, t) {
		return {
			$$typeof: u,
			type: e,
			compare: t === void 0 ? null : t
		};
	}, e.startTransition = ae, e.unstable_useCacheRefresh = function() {
		return T.H.useCacheRefresh();
	}, e.use = function(e) {
		return T.H.use(e);
	}, e.useActionState = function(e, t, n) {
		return T.H.useActionState(e, t, n);
	}, e.useCallback = function(e, t) {
		return T.H.useCallback(e, t);
	}, e.useContext = function(e) {
		return T.H.useContext(e);
	}, e.useDebugValue = function() {}, e.useDeferredValue = function(e, t) {
		return T.H.useDeferredValue(e, t);
	}, e.useEffect = function(e, t) {
		return T.H.useEffect(e, t);
	}, e.useEffectEvent = function(e) {
		return T.H.useEffectEvent(e);
	}, e.useId = function() {
		return T.H.useId();
	}, e.useImperativeHandle = function(e, t, n) {
		return T.H.useImperativeHandle(e, t, n);
	}, e.useInsertionEffect = function(e, t) {
		return T.H.useInsertionEffect(e, t);
	}, e.useLayoutEffect = function(e, t) {
		return T.H.useLayoutEffect(e, t);
	}, e.useMemo = function(e, t) {
		return T.H.useMemo(e, t);
	}, e.useOptimistic = function(e, t) {
		return T.H.useOptimistic(e, t);
	}, e.useReducer = function(e, t, n) {
		return T.H.useReducer(e, t, n);
	}, e.useRef = function(e) {
		return T.H.useRef(e);
	}, e.useState = function(e) {
		return T.H.useState(e);
	}, e.useSyncExternalStore = function(e, t, n) {
		return T.H.useSyncExternalStore(e, t, n);
	}, e.useTransition = function() {
		return T.H.useTransition();
	}, e.version = "19.3.0";
})), u = /* @__PURE__ */ o(((e, t) => {
	t.exports = l();
})), d = /* @__PURE__ */ o(((e) => {
	function t(e, t) {
		var n = e.length;
		e.push(t);
		a: for (; 0 < n;) {
			var r = n - 1 >>> 1, a = e[r];
			if (0 < i(a, t)) e[r] = t, e[n] = a, n = r;
			else break a;
		}
	}
	function n(e) {
		return e.length === 0 ? null : e[0];
	}
	function r(e) {
		if (e.length === 0) return null;
		var t = e[0], n = e.pop();
		if (n !== t) {
			e[0] = n;
			a: for (var r = 0, a = e.length, o = a >>> 1; r < o;) {
				var s = 2 * (r + 1) - 1, c = e[s], l = s + 1, u = e[l];
				if (0 > i(c, n)) l < a && 0 > i(u, c) ? (e[r] = u, e[l] = n, r = l) : (e[r] = c, e[s] = n, r = s);
				else if (l < a && 0 > i(u, n)) e[r] = u, e[l] = n, r = l;
				else break a;
			}
		}
		return t;
	}
	function i(e, t) {
		var n = e.sortIndex - t.sortIndex;
		return n === 0 ? e.id - t.id : n;
	}
	if (e.unstable_now = void 0, typeof performance == "object" && typeof performance.now == "function") {
		var a = performance;
		e.unstable_now = function() {
			return a.now();
		};
	} else {
		var o = Date, s = o.now();
		e.unstable_now = function() {
			return o.now() - s;
		};
	}
	var c = [], l = [], u = 1, d = null, f = 3, p = !1, m = !1, h = !1, g = !1, _ = typeof setTimeout == "function" ? setTimeout : null, v = typeof clearTimeout == "function" ? clearTimeout : null, y = typeof setImmediate < "u" ? setImmediate : null;
	function b(e) {
		for (var i = n(l); i !== null;) {
			if (i.callback === null) r(l);
			else if (i.startTime <= e) r(l), i.sortIndex = i.expirationTime, t(c, i);
			else break;
			i = n(l);
		}
	}
	function x(e) {
		if (h = !1, b(e), !m) {
			if (n(c) !== null) m = !0, S || (S = !0, O());
			else {
				var t = n(l);
				t !== null && A(x, t.startTime - e);
			}
		}
	}
	var S = !1, C = -1, w = 5, T = -1;
	function E() {
		return g ? !0 : !(e.unstable_now() - T < w);
	}
	function D() {
		if (g = !1, S) {
			var t = e.unstable_now();
			T = t;
			var i = !0;
			try {
				a: {
					m = !1, h && (h = !1, v(C), C = -1), p = !0;
					var a = f;
					try {
						b: {
							for (b(t), d = n(c); d !== null && !(d.expirationTime > t && E());) {
								var o = d.callback;
								if (typeof o == "function") {
									d.callback = null, f = d.priorityLevel;
									var s = o(d.expirationTime <= t);
									if (t = e.unstable_now(), typeof s == "function") {
										d.callback = s, b(t), i = !0;
										break b;
									}
									d === n(c) && r(c), b(t);
								} else r(c);
								d = n(c);
							}
							if (d !== null) i = !0;
							else {
								var u = n(l);
								u !== null && A(x, u.startTime - t), i = !1;
							}
						}
						break a;
					} finally {
						d = null, f = a, p = !1;
					}
					i = void 0;
				}
			} finally {
				i ? O() : S = !1;
			}
		}
	}
	var O;
	if (typeof y == "function") O = function() {
		y(D);
	};
	else if (typeof MessageChannel < "u") {
		var k = new MessageChannel(), ee = k.port2;
		k.port1.onmessage = D, O = function() {
			ee.postMessage(null);
		};
	} else O = function() {
		_(D, 0);
	};
	function A(t, n) {
		C = _(function() {
			t(e.unstable_now());
		}, n);
	}
	e.unstable_IdlePriority = 5, e.unstable_ImmediatePriority = 1, e.unstable_LowPriority = 4, e.unstable_NormalPriority = 3, e.unstable_Profiling = null, e.unstable_UserBlockingPriority = 2, e.unstable_cancelCallback = function(e) {
		e.callback = null;
	}, e.unstable_forceFrameRate = function(e) {
		0 > e || 125 < e ? console.error("forceFrameRate takes a positive int between 0 and 125, forcing frame rates higher than 125 fps is not supported") : w = 0 < e ? Math.floor(1e3 / e) : 5;
	}, e.unstable_getCurrentPriorityLevel = function() {
		return f;
	}, e.unstable_next = function(e) {
		switch (f) {
			case 1:
			case 2:
			case 3:
				var t = 3;
				break;
			default: t = f;
		}
		var n = f;
		f = t;
		try {
			return e();
		} finally {
			f = n;
		}
	}, e.unstable_requestPaint = function() {
		g = !0;
	}, e.unstable_runWithPriority = function(e, t) {
		switch (e) {
			case 1:
			case 2:
			case 3:
			case 4:
			case 5: break;
			default: e = 3;
		}
		var n = f;
		f = e;
		try {
			return t();
		} finally {
			f = n;
		}
	}, e.unstable_scheduleCallback = function(r, i, a) {
		var o = e.unstable_now();
		switch (typeof a == "object" && a ? (a = a.delay, a = typeof a == "number" && 0 < a ? o + a : o) : a = o, r) {
			case 1:
				var s = -1;
				break;
			case 2:
				s = 250;
				break;
			case 5:
				s = 1073741823;
				break;
			case 4:
				s = 1e4;
				break;
			default: s = 5e3;
		}
		return s = a + s, r = {
			id: u++,
			callback: i,
			priorityLevel: r,
			startTime: a,
			expirationTime: s,
			sortIndex: -1
		}, a > o ? (r.sortIndex = a, t(l, r), n(c) === null && r === n(l) && (h ? (v(C), C = -1) : h = !0, A(x, a - o))) : (r.sortIndex = s, t(c, r), m || p || (m = !0, S || (S = !0, O()))), r;
	}, e.unstable_shouldYield = E, e.unstable_wrapCallback = function(e) {
		var t = f;
		return function() {
			var n = f;
			f = t;
			try {
				return e.apply(this, arguments);
			} finally {
				f = n;
			}
		};
	};
})), f = /* @__PURE__ */ o(((e, t) => {
	t.exports = d();
})), p = /* @__PURE__ */ o(((e) => {
	var t = u();
	function n(e) {
		var t = "https://react.dev/errors/" + e;
		if (1 < arguments.length) {
			t += "?args[]=" + encodeURIComponent(arguments[1]);
			for (var n = 2; n < arguments.length; n++) t += "&args[]=" + encodeURIComponent(arguments[n]);
		}
		return "Minified React error #" + e + "; visit " + t + " for the full message or use the non-minified dev environment for full errors and additional helpful warnings.";
	}
	function r() {}
	var i = {
		d: {
			f: r,
			r: function() {
				throw Error(n(522));
			},
			D: r,
			C: r,
			L: r,
			m: r,
			X: r,
			S: r,
			M: r
		},
		p: 0,
		findDOMNode: null
	}, a = Symbol.for("react.portal"), o = Symbol.for("react.recoverable"), s = Symbol.for("react.optimistic_key");
	function c(e, t, n) {
		var r = 3 < arguments.length && arguments[3] !== void 0 ? arguments[3] : null;
		return {
			$$typeof: a,
			key: r == null ? null : r === s ? s : "" + r,
			children: e,
			containerInfo: t,
			implementation: n
		};
	}
	var l = t.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE;
	function d(e, t) {
		if (e === "font") return "";
		if (typeof t == "string") return t === "use-credentials" ? t : "";
	}
	e.__DOM_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE = i, e.browser = function(e) {
		return {
			$$typeof: o,
			_reason: e
		};
	}, e.createPortal = function(e, t) {
		var r = 2 < arguments.length && arguments[2] !== void 0 ? arguments[2] : null;
		if (!t || t.nodeType !== 1 && t.nodeType !== 9 && t.nodeType !== 11) throw Error(n(299));
		return c(e, t, null, r);
	}, e.flushSync = function(e) {
		var t = l.T, n = i.p;
		try {
			if (l.T = null, i.p = 2, e) return e();
		} finally {
			l.T = t, i.p = n, i.d.f();
		}
	}, e.preconnect = function(e, t) {
		typeof e == "string" && (t ? (t = t.crossOrigin, t = typeof t == "string" ? t === "use-credentials" ? t : "" : void 0) : t = null, i.d.C(e, t));
	}, e.prefetchDNS = function(e) {
		typeof e == "string" && i.d.D(e);
	}, e.preinit = function(e, t) {
		if (typeof e == "string" && t && typeof t.as == "string") {
			var n = t.as, r = d(n, t.crossOrigin), a = typeof t.integrity == "string" ? t.integrity : void 0, o = typeof t.fetchPriority == "string" ? t.fetchPriority : void 0;
			n === "style" ? i.d.S(e, typeof t.precedence == "string" ? t.precedence : void 0, {
				crossOrigin: r,
				integrity: a,
				fetchPriority: o
			}) : n === "script" && i.d.X(e, {
				crossOrigin: r,
				integrity: a,
				fetchPriority: o,
				nonce: typeof t.nonce == "string" ? t.nonce : void 0
			});
		}
	}, e.preinitModule = function(e, t) {
		if (typeof e == "string") {
			if (typeof t == "object" && t) {
				if (t.as == null || t.as === "script") {
					var n = d(t.as, t.crossOrigin);
					i.d.M(e, {
						crossOrigin: n,
						integrity: typeof t.integrity == "string" ? t.integrity : void 0,
						nonce: typeof t.nonce == "string" ? t.nonce : void 0,
						fetchPriority: typeof t.fetchPriority == "string" ? t.fetchPriority : void 0
					});
				}
			} else t ?? i.d.M(e);
		}
	}, e.preload = function(e, t) {
		if (typeof e == "string" && typeof t == "object" && t && typeof t.as == "string") {
			var n = t.as, r = d(n, t.crossOrigin);
			i.d.L(e, n, {
				crossOrigin: r,
				integrity: typeof t.integrity == "string" ? t.integrity : void 0,
				nonce: typeof t.nonce == "string" ? t.nonce : void 0,
				type: typeof t.type == "string" ? t.type : void 0,
				fetchPriority: typeof t.fetchPriority == "string" ? t.fetchPriority : void 0,
				referrerPolicy: typeof t.referrerPolicy == "string" ? t.referrerPolicy : void 0,
				imageSrcSet: typeof t.imageSrcSet == "string" ? t.imageSrcSet : void 0,
				imageSizes: typeof t.imageSizes == "string" ? t.imageSizes : void 0,
				media: typeof t.media == "string" ? t.media : void 0
			});
		}
	}, e.preloadModule = function(e, t) {
		if (typeof e == "string") {
			if (t) {
				var n = d(t.as, t.crossOrigin);
				i.d.m(e, {
					as: typeof t.as == "string" && t.as !== "script" ? t.as : void 0,
					crossOrigin: n,
					integrity: typeof t.integrity == "string" ? t.integrity : void 0,
					nonce: typeof t.nonce == "string" ? t.nonce : void 0,
					fetchPriority: typeof t.fetchPriority == "string" ? t.fetchPriority : void 0
				});
			} else i.d.m(e);
		}
	}, e.requestFormReset = function(e) {
		i.d.r(e);
	}, e.unstable_batchedUpdates = function(e, t) {
		return e(t);
	}, e.useFormState = function(e, t, n) {
		return l.H.useFormState(e, t, n);
	}, e.useFormStatus = function() {
		return l.H.useHostTransitionStatus();
	}, e.version = "19.3.0";
})), m = /* @__PURE__ */ o(((e, t) => {
	function n() {
		if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u" && typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE == "function") try {
			__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(n);
		} catch (e) {
			console.error(e);
		}
	}
	n(), t.exports = p();
})), h = /* @__PURE__ */ o(((e) => {
	var t = f(), n = u(), r = m();
	function i(e) {
		var t = "https://react.dev/errors/" + e;
		if (1 < arguments.length) {
			t += "?args[]=" + encodeURIComponent(arguments[1]);
			for (var n = 2; n < arguments.length; n++) t += "&args[]=" + encodeURIComponent(arguments[n]);
		}
		return "Minified React error #" + e + "; visit " + t + " for the full message or use the non-minified dev environment for full errors and additional helpful warnings.";
	}
	function a(e) {
		return !(!e || e.nodeType !== 1 && e.nodeType !== 9 && e.nodeType !== 11);
	}
	function o(e) {
		for (var t = e, n = t; n && !n.alternate;) t = n, t.flags & 4098 && (e = t.return), n = t.return;
		for (; t.return;) t = t.return;
		return t.tag === 3 ? e : null;
	}
	function s(e) {
		if (e.tag === 13) {
			var t = e.memoizedState;
			if (t === null && (e = e.alternate, e !== null && (t = e.memoizedState)), t !== null) return t.dehydrated;
		}
		return null;
	}
	function c(e) {
		if (e.tag === 31) {
			var t = e.memoizedState;
			if (t === null && (e = e.alternate, e !== null && (t = e.memoizedState)), t !== null) return t.dehydrated;
		}
		return null;
	}
	function l(e) {
		if (o(e) !== e) throw Error(i(188));
	}
	function d(e) {
		var t = e.alternate;
		if (!t) {
			if (t = o(e), t === null) throw Error(i(188));
			return t === e ? e : null;
		}
		for (var n = e, r = t;;) {
			var a = n.return;
			if (a === null) break;
			var s = a.alternate;
			if (s === null) {
				if (r = a.return, r !== null) {
					n = r;
					continue;
				}
				break;
			}
			if (a.child === s.child) {
				for (s = a.child; s;) {
					if (s === n) return l(a), e;
					if (s === r) return l(a), t;
					s = s.sibling;
				}
				throw Error(i(188));
			}
			if (n.return !== r.return) n = a, r = s;
			else {
				for (var c = !1, u = a.child; u;) {
					if (u === n) {
						c = !0, n = a, r = s;
						break;
					}
					if (u === r) {
						c = !0, r = a, n = s;
						break;
					}
					u = u.sibling;
				}
				if (!c) {
					for (u = s.child; u;) {
						if (u === n) {
							c = !0, n = s, r = a;
							break;
						}
						if (u === r) {
							c = !0, r = s, n = a;
							break;
						}
						u = u.sibling;
					}
					if (!c) throw Error(i(189));
				}
			}
			if (n.alternate !== r) throw Error(i(190));
		}
		if (n.tag !== 3) throw Error(i(188));
		return n.stateNode.current === n ? e : t;
	}
	function p(e) {
		var t = e.tag;
		if (t === 5 || t === 26 || t === 27 || t === 6) return e;
		for (e = e.child; e !== null;) {
			if (t = p(e), t !== null) return t;
			e = e.sibling;
		}
		return null;
	}
	function h(e, t, n, r, i, a) {
		for (; e !== null;) {
			if ((e.tag === 5 || e.tag === 27 || e.tag === 6) && n(e, r, i, a) || (e.tag !== 22 || e.memoizedState === null) && (t || e.tag !== 5 && e.tag !== 27) && h(e.child, t, n, r, i, a)) return !0;
			e = e.sibling;
		}
		return !1;
	}
	function g(e) {
		for (e = e.return; e !== null;) {
			if (e.tag === 3 || e.tag === 5 || e.tag === 27) return e;
			e = e.return;
		}
		return null;
	}
	function _(e) {
		var t = !1;
		for (e = e.return; e !== null && (e.tag === 4 && (t = !0), e.tag !== 3 && e.tag !== 5 && e.tag !== 27);) e = e.return;
		return t;
	}
	function v(e) {
		var t = [null, null], n = g(e);
		return n === null || y(t, e, n.child, { foundSelf: !1 }), t;
	}
	function y(e, t, n, r) {
		for (; n !== null;) {
			if (n === t) r.foundSelf = !0;
			else if (n.tag === 5 || n.tag === 27 || n.tag === 6) {
				if (r.foundSelf) return e[1] = n, !0;
				e[0] = n;
			} else if ((n.tag !== 22 || n.memoizedState === null) && y(e, t, n.child, r)) return !0;
			n = n.sibling;
		}
		return !1;
	}
	function b(e) {
		switch (e.tag) {
			case 5:
			case 27:
			case 6: return e.stateNode;
			case 3: return e.stateNode.containerInfo;
			default: throw Error(i(559));
		}
	}
	var x = null, S = null;
	function C(e, t, n) {
		return e === n || e === t && (x = e, !0);
	}
	function w(e, t, n) {
		return e === n ? (S = e, !1) : e === t && (S !== null && (x = e), !0);
	}
	function T(e) {
		if (e === null) return null;
		do
			e = e === null ? null : e.return;
		while (e && e.tag !== 5 && e.tag !== 27 && e.tag !== 3);
		return e || null;
	}
	function E(e, t, n) {
		for (var r = 0, i = e; i; i = n(i)) r++;
		i = 0;
		for (var a = t; a; a = n(a)) i++;
		for (; 0 < r - i;) e = n(e), r--;
		for (; 0 < i - r;) t = n(t), i--;
		for (; r--;) {
			if (e === t || t !== null && e === t.alternate) return e;
			e = n(e), t = n(t);
		}
		return null;
	}
	var D = Object.assign, O = Symbol.for("react.element"), k = Symbol.for("react.transitional.element"), ee = Symbol.for("react.portal"), A = Symbol.for("react.fragment"), te = Symbol.for("react.strict_mode"), ne = Symbol.for("react.profiler"), re = Symbol.for("react.consumer"), j = Symbol.for("react.context"), M = Symbol.for("react.forward_ref"), ie = Symbol.for("react.suspense"), ae = Symbol.for("react.suspense_list"), oe = Symbol.for("react.memo"), se = Symbol.for("react.lazy"), ce = Symbol.for("react.activity"), le = Symbol.for("react.legacy_hidden"), ue = Symbol.for("react.memo_cache_sentinel"), de = Symbol.for("react.view_transition"), fe = Symbol.for("react.recoverable"), pe = Symbol.iterator;
	function me(e) {
		return typeof e != "object" || !e ? null : (e = pe && e[pe] || e["@@iterator"], typeof e == "function" ? e : null);
	}
	var he = Symbol.for("react.client.reference");
	function ge(e) {
		if (e == null) return null;
		if (typeof e == "function") return e.$$typeof === he ? null : e.displayName || e.name || null;
		if (typeof e == "string") return e;
		switch (e) {
			case A: return "Fragment";
			case ne: return "Profiler";
			case te: return "StrictMode";
			case ie: return "Suspense";
			case ae: return "SuspenseList";
			case ce: return "Activity";
			case de: return "ViewTransition";
		}
		if (typeof e == "object") switch (e.$$typeof) {
			case ee: return "Portal";
			case j: return e.displayName || "Context";
			case re: return (e._context.displayName || "Context") + ".Consumer";
			case M:
				var t = e.render;
				return e = e.displayName, e ||= (e = t.displayName || t.name || "", e === "" ? "ForwardRef" : "ForwardRef(" + e + ")"), e;
			case oe: return t = e.displayName || null, t === null ? ge(e.type) || "Memo" : t;
			case se:
				t = e._payload, e = e._init;
				try {
					return ge(e(t));
				} catch {}
		}
		return null;
	}
	var _e = Array.isArray, N = n.__CLIENT_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE, P = r.__DOM_INTERNALS_DO_NOT_USE_OR_WARN_USERS_THEY_CANNOT_UPGRADE, ve = {
		pending: !1,
		data: null,
		method: null,
		action: null
	}, ye = [], be = -1;
	function xe(e) {
		return { current: e };
	}
	function Se(e) {
		0 > be || (e.current = ye[be], ye[be] = null, be--);
	}
	function F(e, t) {
		be++, ye[be] = e.current, e.current = t;
	}
	var Ce = xe(null), we = xe(null), Te = xe(null), Ee = xe(null);
	function De(e, t) {
		switch (F(Te, t), F(we, e), F(Ce, null), t.nodeType) {
			case 9:
			case 11:
				e = (e = t.documentElement) && (e = e.namespaceURI) ? up(e) : 0;
				break;
			default: if (e = t.tagName, t = t.namespaceURI) t = up(t), e = dp(t, e);
			else switch (e) {
				case "svg":
					e = 1;
					break;
				case "math":
					e = 2;
					break;
				default: e = 0;
			}
		}
		Se(Ce), F(Ce, e);
	}
	function Oe() {
		Se(Ce), Se(we), Se(Te);
	}
	function ke(e) {
		var t = e.memoizedState;
		t !== null && (sh._currentValue = t.memoizedState, F(Ee, e)), t = Ce.current;
		var n = dp(t, e.type);
		t !== n && (F(we, e), F(Ce, n));
	}
	function Ae(e) {
		we.current === e && (Se(Ce), Se(we)), Ee.current === e && (Se(Ee), sh._currentValue = ve);
	}
	var je, Me;
	function Ne(e) {
		if (je === void 0) try {
			throw Error();
		} catch (e) {
			var t = e.stack.trim().match(/\n( *(at )?)/);
			je = t && t[1] || "", Me = -1 < e.stack.indexOf("\n    at") ? " (<anonymous>)" : -1 < e.stack.indexOf("@") ? "@unknown:0:0" : "";
		}
		return "\n" + je + e + Me;
	}
	var Pe = !1;
	function Fe(e, t) {
		if (!e || Pe) return "";
		Pe = !0;
		var n = Error.prepareStackTrace;
		Error.prepareStackTrace = void 0;
		try {
			var r = { DetermineComponentFrameRoot: function() {
				try {
					if (t) {
						var n = function() {
							throw Error();
						};
						if (Object.defineProperty(n.prototype, "props", { set: function() {
							throw Error();
						} }), typeof Reflect == "object" && Reflect.construct) {
							try {
								Reflect.construct(n, []);
							} catch (e) {
								var r = e;
							}
							Reflect.construct(e, [], n);
						} else {
							try {
								n.call();
							} catch (e) {
								r = e;
							}
							n = !1;
							try {
								var i = Object.getOwnPropertyDescriptor(e.prototype, "props");
								Object.defineProperty(e.prototype, "props", {
									configurable: !0,
									set: function() {
										throw Error();
									}
								}), n = !0, new e();
							} finally {
								n && (i === void 0 ? delete e.prototype.props : Object.defineProperty(e.prototype, "props", i));
							}
						}
					} else {
						try {
							throw Error();
						} catch (e) {
							r = e;
						}
						(n = e()) && typeof n.catch == "function" && n.catch(function() {});
					}
				} catch (e) {
					if (e && r && typeof e.stack == "string") return [e.stack, r.stack];
				}
				return [null, null];
			} };
			r.DetermineComponentFrameRoot.displayName = "DetermineComponentFrameRoot";
			var i = Object.getOwnPropertyDescriptor(r.DetermineComponentFrameRoot, "name");
			i && i.configurable && Object.defineProperty(r.DetermineComponentFrameRoot, "name", { value: "DetermineComponentFrameRoot" });
			var a = r.DetermineComponentFrameRoot(), o = a[0], s = a[1];
			if (o && s) {
				var c = o.split("\n"), l = s.split("\n");
				for (i = r = 0; r < c.length && !c[r].includes("DetermineComponentFrameRoot");) r++;
				for (; i < l.length && !l[i].includes("DetermineComponentFrameRoot");) i++;
				if (r === c.length || i === l.length) for (r = c.length - 1, i = l.length - 1; 1 <= r && 0 <= i && c[r] !== l[i];) i--;
				for (; 1 <= r && 0 <= i; r--, i--) if (c[r] !== l[i]) {
					if (r !== 1 || i !== 1) do
						if (r--, i--, 0 > i || c[r] !== l[i]) {
							var u = "\n" + c[r].replace(" at new ", " at ");
							return e.displayName && u.includes("<anonymous>") && (u = u.replace("<anonymous>", e.displayName)), u;
						}
					while (1 <= r && 0 <= i);
					break;
				}
			}
		} finally {
			Pe = !1, Error.prepareStackTrace = n;
		}
		return (n = e ? e.displayName || e.name : "") ? Ne(n) : "";
	}
	function Ie(e, t) {
		switch (e.tag) {
			case 26:
			case 27:
			case 5: return Ne(e.type);
			case 16: return Ne("Lazy");
			case 13: return e.child !== t && t !== null ? Ne("Suspense Fallback") : Ne("Suspense");
			case 19: return Ne("SuspenseList");
			case 0:
			case 15: return Fe(e.type, !1);
			case 11: return Fe(e.type.render, !1);
			case 1: return Fe(e.type, !0);
			case 31: return Ne("Activity");
			case 30: return Ne("ViewTransition");
			default: return "";
		}
	}
	function Le(e) {
		try {
			var t = "", n = null;
			do
				t += Ie(e, n), n = e, e = e.return;
			while (e);
			return t;
		} catch (e) {
			return "\nError generating stack: " + e.message + "\n" + e.stack;
		}
	}
	var Re = Object.prototype.hasOwnProperty, ze = t.unstable_scheduleCallback, Be = t.unstable_cancelCallback, Ve = t.unstable_shouldYield, He = t.unstable_requestPaint, Ue = t.unstable_now, We = t.unstable_getCurrentPriorityLevel, Ge = t.unstable_ImmediatePriority, Ke = t.unstable_UserBlockingPriority, qe = t.unstable_NormalPriority, Je = t.unstable_LowPriority, Ye = t.unstable_IdlePriority, Xe = t.log, Ze = t.unstable_setDisableYieldValue, Qe = null, $e = null;
	function et(e) {
		if (typeof Xe == "function" && Ze(e), $e && typeof $e.setStrictMode == "function") try {
			$e.setStrictMode(Qe, e);
		} catch {}
	}
	var tt = Math.clz32 ? Math.clz32 : it, nt = Math.log, rt = Math.LN2;
	function it(e) {
		return e >>>= 0, e === 0 ? 32 : 31 - (nt(e) / rt | 0) | 0;
	}
	var at = 256, ot = 262144, st = 4194304;
	function ct(e) {
		var t = e & 42;
		if (t !== 0) return t;
		switch (e & -e) {
			case 1: return 1;
			case 2: return 2;
			case 4: return 4;
			case 8: return 8;
			case 16: return 16;
			case 32: return 32;
			case 64: return 64;
			case 128: return 128;
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072: return e & -e;
			case 262144:
			case 524288:
			case 1048576:
			case 2097152: return e & 3932160;
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432: return e & 62914560;
			case 67108864: return 67108864;
			case 134217728: return 134217728;
			case 268435456: return 268435456;
			case 536870912: return 536870912;
			case 1073741824: return 0;
			default: return e;
		}
	}
	function lt(e, t, n) {
		var r = e.pendingLanes;
		if (r === 0) return 0;
		var i = 0, a = e.suspendedLanes, o = e.pingedLanes;
		e = e.warmLanes;
		var s = r & 134217727;
		return s === 0 ? (s = r & ~a, s === 0 ? o === 0 ? n || (n = r & ~e, n !== 0 && (i = ct(n))) : i = ct(o) : i = ct(s)) : (r = s & ~a, r === 0 ? (o &= s, o === 0 ? n || (n = s & ~e, n !== 0 && (i = ct(n))) : i = ct(o)) : i = ct(r)), i === 0 ? 0 : t !== 0 && t !== i && (t & a) === 0 && (a = i & -i, n = t & -t, a >= n || a === 32 && n & 4194048) ? t : i;
	}
	function ut(e, t) {
		return (e.pendingLanes & ~(e.suspendedLanes & ~e.pingedLanes) & t) === 0;
	}
	function dt(e, t) {
		t & 8 && (t |= t & 32);
		var n = e.entangledLanes;
		if (n !== 0) for (e = e.entanglements, n &= t; 0 < n;) {
			var r = 31 - tt(n), i = 1 << r;
			t |= e[r], n &= ~i;
		}
		return t;
	}
	function ft(e, t) {
		switch (e) {
			case 1:
			case 2:
			case 4:
			case 8:
			case 64: return t + 250;
			case 16:
			case 32:
			case 128:
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072:
			case 262144:
			case 524288:
			case 1048576:
			case 2097152: return t + 5e3;
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432: return -1;
			case 67108864:
			case 134217728:
			case 268435456:
			case 536870912:
			case 1073741824: return -1;
			default: return -1;
		}
	}
	function pt() {
		var e = st;
		return st <<= 1, !(st & 62914560) && (st = 4194304), e;
	}
	function mt(e) {
		for (var t = [], n = 0; 31 > n; n++) t.push(e);
		return t;
	}
	function ht(e, t) {
		e.pendingLanes |= t, t !== 268435456 && (e.suspendedLanes = 0, e.pingedLanes = 0, e.warmLanes = 0);
	}
	function gt(e, t, n, r, i, a) {
		var o = e.pendingLanes;
		e.pendingLanes = n, e.suspendedLanes = 0, e.pingedLanes = 0, e.warmLanes = 0, e.expiredLanes &= n, e.entangledLanes &= n, e.errorRecoveryDisabledLanes &= n, e.shellSuspendCounter = 0;
		var s = e.entanglements, c = e.expirationTimes, l = e.hiddenUpdates;
		for (n = o & ~n; 0 < n;) {
			var u = 31 - tt(n), d = 1 << u;
			s[u] = 0, c[u] = -1;
			var f = l[u];
			if (f !== null) for (l[u] = null, u = 0; u < f.length; u++) {
				var p = f[u];
				p !== null && (p.lane &= -536870913);
			}
			n &= ~d;
		}
		r !== 0 && _t(e, r, 0), a !== 0 && i === 0 && e.tag !== 0 && (e.suspendedLanes |= a & ~(o & ~t));
	}
	function _t(e, t, n) {
		e.pendingLanes |= t, e.suspendedLanes &= ~t;
		var r = 31 - tt(t);
		e.entangledLanes |= t, e.entanglements[r] = e.entanglements[r] | 1073741824 | n & 261930;
	}
	function vt(e, t) {
		var n = e.entangledLanes |= t;
		for (e = e.entanglements; n;) {
			var r = 31 - tt(n), i = 1 << r;
			i & t | e[r] & t && (e[r] |= t), n &= ~i;
		}
	}
	function yt(e, t) {
		var n = t & -t;
		return n = n & 42 ? 1 : bt(n), (n & (e.suspendedLanes | t)) === 0 ? n : 0;
	}
	function bt(e) {
		switch (e) {
			case 2:
				e = 1;
				break;
			case 8:
				e = 4;
				break;
			case 32:
				e = 16;
				break;
			case 256:
			case 512:
			case 1024:
			case 2048:
			case 4096:
			case 8192:
			case 16384:
			case 32768:
			case 65536:
			case 131072:
			case 262144:
			case 524288:
			case 1048576:
			case 2097152:
			case 4194304:
			case 8388608:
			case 16777216:
			case 33554432:
				e = 128;
				break;
			case 268435456:
				e = 134217728;
				break;
			default: e = 0;
		}
		return e;
	}
	function xt(e) {
		return e &= -e, 2 < e ? 8 < e ? e & 134217727 ? 32 : 268435456 : 8 : 2;
	}
	function St() {
		var e = P.p;
		return e === 0 ? (e = window.event, e === void 0 ? 32 : Ch(e.type)) : e;
	}
	function Ct(e, t) {
		var n = P.p;
		try {
			return P.p = e, t();
		} finally {
			P.p = n;
		}
	}
	var wt = Math.random().toString(36).slice(2), I = "__reactFiber$" + wt, Tt = "__reactProps$" + wt, Et = "__reactContainer$" + wt, Dt = "__reactEvents$" + wt, Ot = "__reactListeners$" + wt, kt = "__reactHandles$" + wt, At = "__reactResources$" + wt, jt = "__reactMarker$" + wt, Mt = "__reactLoad$" + wt;
	function Nt(e) {
		delete e[I], delete e[Tt], delete e[Ot], delete e[kt];
	}
	function Pt(e) {
		var t;
		if (t = e[I]) return t;
		for (var n = e.parentNode; n;) {
			if (t = n[Et] || n[I]) {
				if (n = t.alternate, t.child !== null || n !== null && n.child !== null) for (e = fm(e); e !== null;) {
					if (n = e[I]) return n;
					e = fm(e);
				}
				return t;
			}
			e = n, n = e.parentNode;
		}
		return null;
	}
	function Ft(e) {
		if (e = e[I] || e[Et]) {
			var t = e.tag;
			if (t === 5 || t === 6 || t === 13 || t === 31 || t === 26 || t === 27 || t === 3) return e;
		}
		return null;
	}
	function It(e) {
		var t = e.tag;
		if (t === 5 || t === 26 || t === 27 || t === 6) return e.stateNode;
		throw Error(i(33));
	}
	function Lt(e) {
		var t = e[At];
		return t ||= e[At] = {
			hoistableStyles: /* @__PURE__ */ new Map(),
			hoistableScripts: /* @__PURE__ */ new Map()
		}, t;
	}
	function Rt(e) {
		e[jt] = !0;
	}
	function zt(e) {
		e[Mt] = void 0;
	}
	var Bt = /* @__PURE__ */ new Set(), Vt = {};
	function Ht(e, t) {
		Ut(e, t), Ut(e + "Capture", t);
	}
	function Ut(e, t) {
		for (Vt[e] = t, e = 0; e < t.length; e++) Bt.add(t[e]);
	}
	var Wt = RegExp("^[:A-Z_a-z\\u00C0-\\u00D6\\u00D8-\\u00F6\\u00F8-\\u02FF\\u0370-\\u037D\\u037F-\\u1FFF\\u200C-\\u200D\\u2070-\\u218F\\u2C00-\\u2FEF\\u3001-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFFD][:A-Z_a-z\\u00C0-\\u00D6\\u00D8-\\u00F6\\u00F8-\\u02FF\\u0370-\\u037D\\u037F-\\u1FFF\\u200C-\\u200D\\u2070-\\u218F\\u2C00-\\u2FEF\\u3001-\\uD7FF\\uF900-\\uFDCF\\uFDF0-\\uFFFD\\-.0-9\\u00B7\\u0300-\\u036F\\u203F-\\u2040]*$"), Gt = {}, Kt = {};
	function qt(e) {
		return Re.call(Kt, e) ? !0 : Re.call(Gt, e) ? !1 : Wt.test(e) ? Kt[e] = !0 : (Gt[e] = !0, !1);
	}
	var L = !1;
	function Jt() {
		var e = L;
		return L = !1, e;
	}
	function Yt(e, t, n) {
		if (qt(t)) {
			if (n === null) e.removeAttribute(t);
			else {
				switch (typeof n) {
					case "undefined":
					case "function":
					case "symbol":
						e.removeAttribute(t);
						return;
					case "boolean":
						var r = t.toLowerCase().slice(0, 5);
						if (r !== "data-" && r !== "aria-") {
							e.removeAttribute(t);
							return;
						}
				}
				e.setAttribute(t, n);
			}
		}
	}
	function Xt(e, t, n) {
		if (n === null) e.removeAttribute(t);
		else {
			switch (typeof n) {
				case "undefined":
				case "function":
				case "symbol":
				case "boolean":
					e.removeAttribute(t);
					return;
			}
			e.setAttribute(t, n);
		}
	}
	function Zt(e, t, n, r) {
		if (r === null) e.removeAttribute(n);
		else {
			switch (typeof r) {
				case "undefined":
				case "function":
				case "symbol":
				case "boolean":
					e.removeAttribute(n);
					return;
			}
			e.setAttributeNS(t, n, r);
		}
	}
	function Qt(e) {
		switch (typeof e) {
			case "bigint":
			case "boolean":
			case "number":
			case "string":
			case "undefined": return e;
			case "object": return e;
			default: return "";
		}
	}
	function $t(e) {
		var t = e.type;
		return (e = e.nodeName) && e.toLowerCase() === "input" && (t === "checkbox" || t === "radio");
	}
	function en(e, t, n) {
		var r = Object.getOwnPropertyDescriptor(e.constructor.prototype, t);
		if (!e.hasOwnProperty(t) && r !== void 0 && typeof r.get == "function" && typeof r.set == "function") {
			var i = r.get, a = r.set;
			return Object.defineProperty(e, t, {
				configurable: !0,
				get: function() {
					return i.call(this);
				},
				set: function(e) {
					n = "" + e, a.call(this, e);
				}
			}), Object.defineProperty(e, t, { enumerable: r.enumerable }), {
				getValue: function() {
					return n;
				},
				setValue: function(e) {
					n = "" + e;
				},
				stopTracking: function() {
					e._valueTracker = null, delete e[t];
				}
			};
		}
	}
	function tn(e) {
		if (!e._valueTracker) {
			var t = $t(e) ? "checked" : "value";
			e._valueTracker = en(e, t, "" + e[t]);
		}
	}
	function nn(e) {
		if (!e) return !1;
		var t = e._valueTracker;
		if (!t) return !0;
		var n = t.getValue(), r = "";
		return e && (r = $t(e) ? e.checked ? "true" : "false" : e.value), e = r, e !== n && (t.setValue(e), !0);
	}
	var rn = /[\n"\\]/g;
	function an(e) {
		return e.replace(rn, function(e) {
			return "\\" + e.charCodeAt(0).toString(16) + " ";
		});
	}
	function on(e, t, n, r, i, a, o, s) {
		e.name = "", o != null && typeof o != "function" && typeof o != "symbol" && typeof o != "boolean" ? e.type = o : e.removeAttribute("type"), t == null ? o !== "submit" && o !== "reset" || e.removeAttribute("value") : o === "number" ? (t === 0 && e.value === "" || e.value != t) && (e.value = "" + Qt(t)) : e.value !== "" + Qt(t) && (e.value = "" + Qt(t)), t == null ? n == null ? r != null && e.removeAttribute("value") : cn(e, Qt(n)) : o === "number" && e.value == t ? cn(e, Qt(e.value)) : cn(e, Qt(t)), i == null && a != null && (e.defaultChecked = !!a), i != null && (e.checked = i && typeof i != "function" && typeof i != "symbol"), s != null && typeof s != "function" && typeof s != "symbol" && typeof s != "boolean" ? e.name = "" + Qt(s) : e.removeAttribute("name");
	}
	function sn(e, t, n, r, i, a, o, s) {
		if (a != null && typeof a != "function" && typeof a != "symbol" && typeof a != "boolean" && (e.type = a), t != null || n != null) {
			if (!(a !== "submit" && a !== "reset" || t != null)) {
				tn(e);
				return;
			}
			n = n == null ? "" : "" + Qt(n), t = t == null ? n : "" + Qt(t), s || t === e.value || (e.value = t), e.defaultValue = t;
		}
		r ??= i, r = typeof r != "function" && typeof r != "symbol" && !!r, e.checked = s ? e.checked : !!r, e.defaultChecked = !!r, o != null && typeof o != "function" && typeof o != "symbol" && typeof o != "boolean" && (e.name = o), tn(e);
	}
	function cn(e, t) {
		e.defaultValue !== "" + t && (e.defaultValue = "" + t);
	}
	function ln(e, t, n, r) {
		if (e = e.options, t) {
			t = {};
			for (var i = 0; i < n.length; i++) t["$" + n[i]] = !0;
			for (n = 0; n < e.length; n++) i = t.hasOwnProperty("$" + e[n].value), e[n].selected !== i && (e[n].selected = i), i && r && (e[n].defaultSelected = !0);
		} else {
			for (n = "" + Qt(n), t = null, i = 0; i < e.length; i++) {
				if (e[i].value === n) {
					e[i].selected = !0, r && (e[i].defaultSelected = !0);
					return;
				}
				t !== null || e[i].disabled || (t = e[i]);
			}
			t !== null && (t.selected = !0);
		}
	}
	function un(e, t, n) {
		if (t != null && (t = "" + Qt(t), t !== e.value && (e.value = t), n == null)) {
			e.defaultValue !== t && (e.defaultValue = t);
			return;
		}
		e.defaultValue = n == null ? "" : "" + Qt(n);
	}
	function dn(e, t, n, r) {
		if (t == null) {
			if (r != null) {
				if (n != null) throw Error(i(92));
				if (_e(r)) {
					if (1 < r.length) throw Error(i(93));
					r = r[0];
				}
				n = r;
			}
			n ??= "", t = n;
		}
		n = Qt(t), e.defaultValue = n, r = e.textContent, r === n && r !== "" && r !== null && (e.value = r), tn(e);
	}
	function fn(e, t) {
		if (t) {
			var n = e.firstChild;
			if (n && n === e.lastChild && n.nodeType === 3) {
				n.nodeValue = t;
				return;
			}
		}
		e.textContent = t;
	}
	var pn = new Set("animationIterationCount aspectRatio borderImageOutset borderImageSlice borderImageWidth boxFlex boxFlexGroup boxOrdinalGroup columnCount columns flex flexGrow flexPositive flexShrink flexNegative flexOrder gridArea gridRow gridRowEnd gridRowSpan gridRowStart gridColumn gridColumnEnd gridColumnSpan gridColumnStart fontWeight lineClamp lineHeight opacity order orphans scale tabSize widows zIndex zoom fillOpacity floodOpacity stopOpacity strokeDasharray strokeDashoffset strokeMiterlimit strokeOpacity strokeWidth MozAnimationIterationCount MozBoxFlex MozBoxFlexGroup MozLineClamp msAnimationIterationCount msFlex msZoom msFlexGrow msFlexNegative msFlexOrder msFlexPositive msFlexShrink msGridColumn msGridColumnSpan msGridRow msGridRowSpan WebkitAnimationIterationCount WebkitBoxFlex WebKitBoxFlexGroup WebkitBoxOrdinalGroup WebkitColumnCount WebkitColumns WebkitFlex WebkitFlexGrow WebkitFlexPositive WebkitFlexShrink WebkitLineClamp".split(" "));
	function mn(e, t, n) {
		var r = t.indexOf("--") === 0;
		n == null || typeof n == "boolean" || n === "" ? r ? e.setProperty(t, "") : t === "float" ? e.cssFloat = "" : e[t] = "" : r ? e.setProperty(t, n) : typeof n != "number" || n === 0 || pn.has(t) ? t === "float" ? e.cssFloat = n : e[t] = ("" + n).trim() : e[t] = n + "px";
	}
	function hn(e, t, n) {
		if (t != null && typeof t != "object") throw Error(i(62));
		if (e = e.style, n != null) {
			for (var r in n) !n.hasOwnProperty(r) || t != null && t.hasOwnProperty(r) || (r.indexOf("--") === 0 ? e.setProperty(r, "") : r === "float" ? e.cssFloat = "" : e[r] = "", L = !0);
			for (var a in t) r = t[a], t.hasOwnProperty(a) && n[a] !== r && (mn(e, a, r), L = !0);
		} else for (var o in t) t.hasOwnProperty(o) && mn(e, o, t[o]);
	}
	function gn(e) {
		if (e.indexOf("-") === -1) return !1;
		switch (e) {
			case "annotation-xml":
			case "color-profile":
			case "font-face":
			case "font-face-src":
			case "font-face-uri":
			case "font-face-format":
			case "font-face-name":
			case "missing-glyph": return !1;
			default: return !0;
		}
	}
	var _n = /* @__PURE__ */ new Map([
		["acceptCharset", "accept-charset"],
		["htmlFor", "for"],
		["httpEquiv", "http-equiv"],
		["crossOrigin", "crossorigin"],
		["accentHeight", "accent-height"],
		["alignmentBaseline", "alignment-baseline"],
		["arabicForm", "arabic-form"],
		["baselineShift", "baseline-shift"],
		["capHeight", "cap-height"],
		["clipPath", "clip-path"],
		["clipRule", "clip-rule"],
		["colorInterpolation", "color-interpolation"],
		["colorInterpolationFilters", "color-interpolation-filters"],
		["colorProfile", "color-profile"],
		["colorRendering", "color-rendering"],
		["dominantBaseline", "dominant-baseline"],
		["enableBackground", "enable-background"],
		["fillOpacity", "fill-opacity"],
		["fillRule", "fill-rule"],
		["floodColor", "flood-color"],
		["floodOpacity", "flood-opacity"],
		["fontFamily", "font-family"],
		["fontSize", "font-size"],
		["fontSizeAdjust", "font-size-adjust"],
		["fontStretch", "font-stretch"],
		["fontStyle", "font-style"],
		["fontVariant", "font-variant"],
		["fontWeight", "font-weight"],
		["glyphName", "glyph-name"],
		["glyphOrientationHorizontal", "glyph-orientation-horizontal"],
		["glyphOrientationVertical", "glyph-orientation-vertical"],
		["horizAdvX", "horiz-adv-x"],
		["horizOriginX", "horiz-origin-x"],
		["imageRendering", "image-rendering"],
		["letterSpacing", "letter-spacing"],
		["lightingColor", "lighting-color"],
		["markerEnd", "marker-end"],
		["markerMid", "marker-mid"],
		["markerStart", "marker-start"],
		["maskType", "mask-type"],
		["overlinePosition", "overline-position"],
		["overlineThickness", "overline-thickness"],
		["paintOrder", "paint-order"],
		["panose-1", "panose-1"],
		["pointerEvents", "pointer-events"],
		["renderingIntent", "rendering-intent"],
		["shapeRendering", "shape-rendering"],
		["stopColor", "stop-color"],
		["stopOpacity", "stop-opacity"],
		["strikethroughPosition", "strikethrough-position"],
		["strikethroughThickness", "strikethrough-thickness"],
		["strokeDasharray", "stroke-dasharray"],
		["strokeDashoffset", "stroke-dashoffset"],
		["strokeLinecap", "stroke-linecap"],
		["strokeLinejoin", "stroke-linejoin"],
		["strokeMiterlimit", "stroke-miterlimit"],
		["strokeOpacity", "stroke-opacity"],
		["strokeWidth", "stroke-width"],
		["textAnchor", "text-anchor"],
		["textDecoration", "text-decoration"],
		["textRendering", "text-rendering"],
		["transformOrigin", "transform-origin"],
		["underlinePosition", "underline-position"],
		["underlineThickness", "underline-thickness"],
		["unicodeBidi", "unicode-bidi"],
		["unicodeRange", "unicode-range"],
		["unitsPerEm", "units-per-em"],
		["vAlphabetic", "v-alphabetic"],
		["vHanging", "v-hanging"],
		["vIdeographic", "v-ideographic"],
		["vMathematical", "v-mathematical"],
		["vectorEffect", "vector-effect"],
		["vertAdvY", "vert-adv-y"],
		["vertOriginX", "vert-origin-x"],
		["vertOriginY", "vert-origin-y"],
		["wordSpacing", "word-spacing"],
		["writingMode", "writing-mode"],
		["xmlnsXlink", "xmlns:xlink"],
		["xHeight", "x-height"]
	]), vn = /^[\u0000-\u001F ]*j[\r\n\t]*a[\r\n\t]*v[\r\n\t]*a[\r\n\t]*s[\r\n\t]*c[\r\n\t]*r[\r\n\t]*i[\r\n\t]*p[\r\n\t]*t[\r\n\t]*:/i;
	function yn(e) {
		return vn.test("" + e) ? "javascript:throw new Error('React has blocked a javascript: URL as a security precaution.')" : e;
	}
	function bn() {}
	var xn = null;
	function Sn(e) {
		return e = e.target || e.srcElement || window, e.correspondingUseElement && (e = e.correspondingUseElement), e.nodeType === 3 ? e.parentNode : e;
	}
	var Cn = null, wn = null;
	function Tn(e) {
		var t = Ft(e);
		if (t && (e = t.stateNode)) {
			var n = e[Tt] || null;
			a: switch (e = t.stateNode, t.type) {
				case "input":
					if (on(e, n.value, n.defaultValue, n.defaultValue, n.checked, n.defaultChecked, n.type, n.name), t = n.name, n.type === "radio" && t != null) {
						for (n = e; n.parentNode;) n = n.parentNode;
						for (n = n.querySelectorAll("input[name=\"" + an("" + t) + "\"][type=\"radio\"]"), t = 0; t < n.length; t++) {
							var r = n[t];
							if (r !== e && r.form === e.form) {
								var a = r[Tt] || null;
								if (!a) throw Error(i(90));
								on(r, a.value, a.defaultValue, a.defaultValue, a.checked, a.defaultChecked, a.type, a.name);
							}
						}
						for (t = 0; t < n.length; t++) r = n[t], r.form === e.form && nn(r);
					}
					break a;
				case "textarea":
					un(e, n.value, n.defaultValue);
					break a;
				case "select": t = n.value, t != null && ln(e, !!n.multiple, t, !1);
			}
		}
	}
	var En = !1;
	function Dn(e, t, n) {
		if (En) return e(t, n);
		En = !0;
		try {
			return e(t);
		} finally {
			if (En = !1, (Cn !== null || wn !== null) && (zd(), Cn && (t = Cn, e = wn, wn = Cn = null, Tn(t), e))) for (t = 0; t < e.length; t++) Tn(e[t]);
		}
	}
	function On(e, t) {
		var n = e.stateNode;
		if (n === null) return null;
		var r = n[Tt] || null;
		if (r === null) return null;
		n = r[t];
		a: switch (t) {
			case "onClick":
			case "onClickCapture":
			case "onDoubleClick":
			case "onDoubleClickCapture":
			case "onMouseDown":
			case "onMouseDownCapture":
			case "onMouseMove":
			case "onMouseMoveCapture":
			case "onMouseUp":
			case "onMouseUpCapture":
			case "onMouseEnter":
				(r = !r.disabled) || (e = e.type, r = e !== "button" && e !== "input" && e !== "select" && e !== "textarea"), e = !r;
				break a;
			default: e = !1;
		}
		if (e) return null;
		if (n && typeof n != "function") throw Error(i(231, t, typeof n));
		return n;
	}
	var kn = typeof window < "u" && window.document !== void 0 && window.document.createElement !== void 0, An = !1;
	if (kn) try {
		var jn = {};
		Object.defineProperty(jn, "passive", { get: function() {
			An = !0;
		} }), window.addEventListener("test", jn, jn), window.removeEventListener("test", jn, jn);
	} catch {
		An = !1;
	}
	var Mn = null, Nn = null, Pn = null;
	function Fn() {
		if (Pn) return Pn;
		var e, t = Nn, n = t.length, r, i = "value" in Mn ? Mn.value : Mn.textContent, a = i.length;
		for (e = 0; e < n && t[e] === i[e]; e++);
		var o = n - e;
		for (r = 1; r <= o && t[n - r] === i[a - r]; r++);
		return Pn = i.slice(e, 1 < r ? 1 - r : void 0);
	}
	function In(e) {
		var t = e.keyCode;
		return "charCode" in e ? (e = e.charCode, e === 0 && t === 13 && (e = 13)) : e = t, e === 10 && (e = 13), 32 <= e || e === 13 ? e : 0;
	}
	function Ln() {
		return !0;
	}
	function Rn() {
		return !1;
	}
	function zn(e) {
		function t(t, n, r, i, a) {
			for (var o in this._reactName = t, this._targetInst = r, this.type = n, this.nativeEvent = i, this.target = a, this.currentTarget = null, e) e.hasOwnProperty(o) && (t = e[o], this[o] = t ? t(i) : i[o]);
			return this.isDefaultPrevented = (i.defaultPrevented == null ? !1 === i.returnValue : i.defaultPrevented) ? Ln : Rn, this.isPropagationStopped = Rn, this;
		}
		return D(t.prototype, {
			preventDefault: function() {
				this.defaultPrevented = !0;
				var e = this.nativeEvent;
				e && (e.preventDefault ? e.preventDefault() : typeof e.returnValue != "unknown" && (e.returnValue = !1), this.isDefaultPrevented = Ln);
			},
			stopPropagation: function() {
				var e = this.nativeEvent;
				e && (e.stopPropagation ? e.stopPropagation() : typeof e.cancelBubble != "unknown" && (e.cancelBubble = !0), this.isPropagationStopped = Ln);
			},
			persist: function() {},
			isPersistent: Ln
		}), t;
	}
	var Bn = {
		eventPhase: 0,
		bubbles: 0,
		cancelable: 0,
		timeStamp: function(e) {
			return e.timeStamp || Date.now();
		},
		defaultPrevented: 0,
		isTrusted: 0
	}, Vn = zn(Bn), Hn = D({}, Bn, {
		view: 0,
		detail: 0
	}), Un = zn(Hn), Wn, Gn, Kn, qn = D({}, Hn, {
		screenX: 0,
		screenY: 0,
		clientX: 0,
		clientY: 0,
		pageX: 0,
		pageY: 0,
		ctrlKey: 0,
		shiftKey: 0,
		altKey: 0,
		metaKey: 0,
		getModifierState: ir,
		button: 0,
		buttons: 0,
		relatedTarget: function(e) {
			return e.relatedTarget === void 0 ? e.fromElement === e.srcElement ? e.toElement : e.fromElement : e.relatedTarget;
		},
		movementX: function(e) {
			return "movementX" in e ? e.movementX : (e !== Kn && (Kn && e.type === "mousemove" ? (Wn = e.screenX - Kn.screenX, Gn = e.screenY - Kn.screenY) : Gn = Wn = 0, Kn = e), Wn);
		},
		movementY: function(e) {
			return "movementY" in e ? e.movementY : Gn;
		}
	}), Jn = zn(qn), Yn = zn(D({}, qn, { dataTransfer: 0 })), Xn = zn(D({}, Hn, { relatedTarget: 0 })), Zn = zn(D({}, Bn, {
		animationName: 0,
		elapsedTime: 0,
		pseudoElement: 0
	})), Qn = zn(D({}, Bn, { clipboardData: function(e) {
		return "clipboardData" in e ? e.clipboardData : window.clipboardData;
	} })), $n = zn(D({}, Bn, { data: 0 })), er = {
		Esc: "Escape",
		Spacebar: " ",
		Left: "ArrowLeft",
		Up: "ArrowUp",
		Right: "ArrowRight",
		Down: "ArrowDown",
		Del: "Delete",
		Win: "OS",
		Menu: "ContextMenu",
		Apps: "ContextMenu",
		Scroll: "ScrollLock",
		MozPrintableKey: "Unidentified"
	}, tr = {
		8: "Backspace",
		9: "Tab",
		12: "Clear",
		13: "Enter",
		16: "Shift",
		17: "Control",
		18: "Alt",
		19: "Pause",
		20: "CapsLock",
		27: "Escape",
		32: " ",
		33: "PageUp",
		34: "PageDown",
		35: "End",
		36: "Home",
		37: "ArrowLeft",
		38: "ArrowUp",
		39: "ArrowRight",
		40: "ArrowDown",
		45: "Insert",
		46: "Delete",
		112: "F1",
		113: "F2",
		114: "F3",
		115: "F4",
		116: "F5",
		117: "F6",
		118: "F7",
		119: "F8",
		120: "F9",
		121: "F10",
		122: "F11",
		123: "F12",
		144: "NumLock",
		145: "ScrollLock",
		224: "Meta"
	}, nr = {
		Alt: "altKey",
		Control: "ctrlKey",
		Meta: "metaKey",
		Shift: "shiftKey"
	};
	function rr(e) {
		var t = this.nativeEvent;
		return t.getModifierState ? t.getModifierState(e) : (e = nr[e]) ? !!t[e] : !1;
	}
	function ir() {
		return rr;
	}
	var ar = zn(D({}, Hn, {
		key: function(e) {
			if (e.key) {
				var t = er[e.key] || e.key;
				if (t !== "Unidentified") return t;
			}
			return e.type === "keypress" ? (e = In(e), e === 13 ? "Enter" : String.fromCharCode(e)) : e.type === "keydown" || e.type === "keyup" ? tr[e.keyCode] || "Unidentified" : "";
		},
		code: 0,
		location: 0,
		ctrlKey: 0,
		shiftKey: 0,
		altKey: 0,
		metaKey: 0,
		repeat: 0,
		locale: 0,
		getModifierState: ir,
		charCode: function(e) {
			return e.type === "keypress" ? In(e) : 0;
		},
		keyCode: function(e) {
			return e.type === "keydown" || e.type === "keyup" ? e.keyCode : 0;
		},
		which: function(e) {
			return e.type === "keypress" ? In(e) : e.type === "keydown" || e.type === "keyup" ? e.keyCode : 0;
		}
	})), or = zn(D({}, qn, {
		pointerId: 0,
		width: 0,
		height: 0,
		pressure: 0,
		tangentialPressure: 0,
		tiltX: 0,
		tiltY: 0,
		twist: 0,
		pointerType: 0,
		isPrimary: 0
	})), sr = zn(D({}, Bn, { submitter: 0 })), cr = zn(D({}, Hn, {
		touches: 0,
		targetTouches: 0,
		changedTouches: 0,
		altKey: 0,
		metaKey: 0,
		ctrlKey: 0,
		shiftKey: 0,
		getModifierState: ir
	})), lr = zn(D({}, Bn, {
		propertyName: 0,
		elapsedTime: 0,
		pseudoElement: 0
	})), ur = zn(D({}, qn, {
		deltaX: function(e) {
			return "deltaX" in e ? e.deltaX : "wheelDeltaX" in e ? -e.wheelDeltaX : 0;
		},
		deltaY: function(e) {
			return "deltaY" in e ? e.deltaY : "wheelDeltaY" in e ? -e.wheelDeltaY : "wheelDelta" in e ? -e.wheelDelta : 0;
		},
		deltaZ: 0,
		deltaMode: 0
	})), dr = zn(D({}, Bn, {
		newState: 0,
		oldState: 0,
		source: 0
	})), fr = [
		9,
		13,
		27,
		32
	], pr = kn && "CompositionEvent" in window, mr = null;
	kn && "documentMode" in document && (mr = document.documentMode);
	var hr = kn && "TextEvent" in window && !mr, gr = kn && (!pr || mr && 8 < mr && 11 >= mr), _r = " ", vr = !1;
	function yr(e, t) {
		switch (e) {
			case "keyup": return fr.indexOf(t.keyCode) !== -1;
			case "keydown": return t.keyCode !== 229;
			case "keypress":
			case "mousedown":
			case "focusout": return !0;
			default: return !1;
		}
	}
	function br(e) {
		return e = e.detail, typeof e == "object" && "data" in e ? e.data : null;
	}
	var xr = !1;
	function Sr(e, t) {
		switch (e) {
			case "compositionend": return br(t);
			case "keypress": return t.which === 32 ? (vr = !0, _r) : null;
			case "textInput": return e = t.data, e === _r && vr ? null : e;
			default: return null;
		}
	}
	function Cr(e, t) {
		if (xr) return e === "compositionend" || !pr && yr(e, t) ? (e = Fn(), Pn = Nn = Mn = null, xr = !1, e) : null;
		switch (e) {
			case "paste": return null;
			case "keypress":
				if (!(t.ctrlKey || t.altKey || t.metaKey) || t.ctrlKey && t.altKey) {
					if (t.char && 1 < t.char.length) return t.char;
					if (t.which) return String.fromCharCode(t.which);
				}
				return null;
			case "compositionend": return gr && t.locale !== "ko" ? null : t.data;
			default: return null;
		}
	}
	var wr = {
		color: !0,
		date: !0,
		datetime: !0,
		"datetime-local": !0,
		email: !0,
		month: !0,
		number: !0,
		password: !0,
		range: !0,
		search: !0,
		tel: !0,
		text: !0,
		time: !0,
		url: !0,
		week: !0
	};
	function Tr(e) {
		var t = e && e.nodeName && e.nodeName.toLowerCase();
		return t === "input" ? !!wr[e.type] : t === "textarea";
	}
	function Er(e, t, n, r) {
		Cn ? wn ? wn.push(r) : wn = [r] : Cn = r, t = Jf(t, "onChange"), 0 < t.length && (n = new Vn("onChange", "change", null, n, r), e.push({
			event: n,
			listeners: t
		}));
	}
	var Dr = null, Or = null;
	function kr(e) {
		Vf(e, 0);
	}
	function Ar(e) {
		if (nn(It(e))) return e;
	}
	function jr(e, t) {
		if (e === "change") return t;
	}
	var Mr = !1;
	if (kn) {
		var Nr;
		if (kn) {
			var Pr = "oninput" in document;
			if (!Pr) {
				var Fr = document.createElement("div");
				Fr.setAttribute("oninput", "return;"), Pr = typeof Fr.oninput == "function";
			}
			Nr = Pr;
		} else Nr = !1;
		Mr = Nr && (!document.documentMode || 9 < document.documentMode);
	}
	function Ir() {
		Dr && (Dr.detachEvent("onpropertychange", Lr), Or = Dr = null);
	}
	function Lr(e) {
		if (e.propertyName === "value" && Ar(Or)) {
			var t = [];
			Er(t, Or, e, Sn(e)), Dn(kr, t);
		}
	}
	function Rr(e, t, n) {
		e === "focusin" ? (Ir(), Dr = t, Or = n, Dr.attachEvent("onpropertychange", Lr)) : e === "focusout" && Ir();
	}
	function zr(e) {
		if (e === "selectionchange" || e === "keyup" || e === "keydown") return Ar(Or);
	}
	function Br(e, t) {
		if (e === "click") return Ar(t);
	}
	function Vr(e, t) {
		if (e === "input" || e === "change") return Ar(t);
	}
	function Hr(e, t) {
		return e === t && (e !== 0 || 1 / e == 1 / t) || e !== e && t !== t;
	}
	var Ur = typeof Object.is == "function" ? Object.is : Hr;
	function Wr(e, t) {
		if (Ur(e, t)) return !0;
		if (typeof e != "object" || !e || typeof t != "object" || !t) return !1;
		var n = Object.keys(e), r = Object.keys(t);
		if (n.length !== r.length) return !1;
		for (r = 0; r < n.length; r++) {
			var i = n[r];
			if (!Re.call(t, i) || !Ur(e[i], t[i])) return !1;
		}
		return !0;
	}
	function Gr(e) {
		if (e ||= typeof document < "u" ? document : void 0, e === void 0) return null;
		try {
			return e.activeElement || e.body;
		} catch {
			return e.body;
		}
	}
	function Kr(e) {
		for (; e && e.firstChild;) e = e.firstChild;
		return e;
	}
	function qr(e, t) {
		var n = Kr(e);
		e = 0;
		for (var r; n;) {
			if (n.nodeType === 3) {
				if (r = e + n.textContent.length, e <= t && r >= t) return {
					node: n,
					offset: t - e
				};
				e = r;
			}
			a: {
				for (; n;) {
					if (n.nextSibling) {
						n = n.nextSibling;
						break a;
					}
					n = n.parentNode;
				}
				n = void 0;
			}
			n = Kr(n);
		}
	}
	function Jr(e, t) {
		return e && t ? e === t ? !0 : e && e.nodeType === 3 ? !1 : t && t.nodeType === 3 ? Jr(e, t.parentNode) : "contains" in e ? e.contains(t) : e.compareDocumentPosition ? !!(e.compareDocumentPosition(t) & 16) : !1 : !1;
	}
	function Yr(e) {
		e = e != null && e.ownerDocument != null && e.ownerDocument.defaultView != null ? e.ownerDocument.defaultView : window;
		for (var t = Gr(e.document); t instanceof e.HTMLIFrameElement;) {
			try {
				var n = typeof t.contentWindow.location.href == "string";
			} catch {
				n = !1;
			}
			if (n) e = t.contentWindow;
			else break;
			t = Gr(e.document);
		}
		return t;
	}
	function Xr(e) {
		var t = e && e.nodeName && e.nodeName.toLowerCase();
		return t && (t === "input" && (e.type === "text" || e.type === "search" || e.type === "tel" || e.type === "url" || e.type === "password") || t === "textarea" || e.contentEditable === "true");
	}
	var Zr = kn && "documentMode" in document && 11 >= document.documentMode, Qr = null, $r = null, ei = null, ti = !1;
	function ni(e, t, n) {
		var r = n.window === n ? n.document : n.nodeType === 9 ? n : n.ownerDocument;
		ti || Qr == null || Qr !== Gr(r) || (r = Qr, "selectionStart" in r && Xr(r) ? r = {
			start: r.selectionStart,
			end: r.selectionEnd
		} : (r = (r.ownerDocument && r.ownerDocument.defaultView || window).getSelection(), r = {
			anchorNode: r.anchorNode,
			anchorOffset: r.anchorOffset,
			focusNode: r.focusNode,
			focusOffset: r.focusOffset
		}), ei && Wr(ei, r) || (ei = r, r = Jf($r, "onSelect"), 0 < r.length && (t = new Vn("onSelect", "select", null, t, n), e.push({
			event: t,
			listeners: r
		}), t.target = Qr)));
	}
	function ri(e, t) {
		var n = {};
		return n[e.toLowerCase()] = t.toLowerCase(), n["Webkit" + e] = "webkit" + t, n["Moz" + e] = "moz" + t, n;
	}
	var ii = {
		animationend: ri("Animation", "AnimationEnd"),
		animationiteration: ri("Animation", "AnimationIteration"),
		animationstart: ri("Animation", "AnimationStart"),
		transitionrun: ri("Transition", "TransitionRun"),
		transitionstart: ri("Transition", "TransitionStart"),
		transitioncancel: ri("Transition", "TransitionCancel"),
		transitionend: ri("Transition", "TransitionEnd")
	}, ai = {}, oi = {};
	kn && (oi = document.createElement("div").style, "AnimationEvent" in window || (delete ii.animationend.animation, delete ii.animationiteration.animation, delete ii.animationstart.animation), "TransitionEvent" in window || delete ii.transitionend.transition);
	function si(e) {
		if (ai[e]) return ai[e];
		if (!ii[e]) return e;
		var t = ii[e], n;
		for (n in t) if (t.hasOwnProperty(n) && n in oi) return ai[e] = t[n];
		return e;
	}
	var ci = si("animationend"), li = si("animationiteration"), ui = si("animationstart"), di = si("transitionrun"), fi = si("transitionstart"), pi = si("transitioncancel"), mi = si("transitionend"), hi = /* @__PURE__ */ new Map(), gi = "abort auxClick beforeToggle cancel canPlay canPlayThrough click close contextMenu copy cut drag dragEnd dragEnter dragExit dragLeave dragOver dragStart drop durationChange emptied encrypted ended error fullscreenChange fullscreenError gotPointerCapture input invalid keyDown keyPress keyUp load loadedData loadedMetadata loadStart lostPointerCapture mouseDown mouseMove mouseOut mouseOver mouseUp paste pause play playing pointerCancel pointerDown pointerMove pointerOut pointerOver pointerUp progress rateChange reset resize seeked seeking stalled submit suspend timeUpdate touchCancel touchEnd touchStart volumeChange scroll toggle touchMove waiting wheel".split(" ");
	gi.push("scrollEnd");
	function _i(e, t) {
		hi.set(e, t), Ht(t, [e]);
	}
	var vi = 0;
	function yi(e, t) {
		if (e.name != null && e.name !== "auto") return e.name;
		if (t.autoName !== null) return t.autoName;
		e = bd.identifierPrefix;
		var n = vi++;
		return e = "_" + e + "t_" + n.toString(32) + "_", t.autoName = e;
	}
	function bi(e) {
		if (e == null || typeof e == "string") return e;
		var t = null, n = Od;
		if (n !== null) for (var r = 0; r < n.length; r++) {
			var i = e[n[r]];
			if (i != null) {
				if (i === "none") return "none";
				t = t == null ? i : t + (" " + i);
			}
		}
		return t ?? e.default;
	}
	function xi(e, t) {
		return e = bi(e), t = bi(t), t == null ? e === "auto" ? null : e : t === "auto" ? null : t;
	}
	var Si = typeof reportError == "function" ? reportError : function(e) {
		if (typeof window == "object" && typeof window.ErrorEvent == "function") {
			var t = new window.ErrorEvent("error", {
				bubbles: !0,
				cancelable: !0,
				message: typeof e == "object" && e && typeof e.message == "string" ? String(e.message) : String(e),
				error: e
			});
			if (!window.dispatchEvent(t)) return;
		} else if (typeof process == "object" && typeof process.emit == "function") {
			process.emit("uncaughtException", e);
			return;
		}
		console.error(e);
	}, Ci = [], wi = 0, Ti = 0;
	function Ei() {
		for (var e = wi, t = Ti = wi = 0; t < e;) {
			var n = Ci[t];
			Ci[t++] = null;
			var r = Ci[t];
			Ci[t++] = null;
			var i = Ci[t];
			Ci[t++] = null;
			var a = Ci[t];
			if (Ci[t++] = null, r !== null && i !== null) {
				var o = r.pending;
				o === null ? i.next = i : (i.next = o.next, o.next = i), r.pending = i;
			}
			a !== 0 && Ai(n, i, a);
		}
	}
	function Di(e, t, n, r) {
		Ci[wi++] = e, Ci[wi++] = t, Ci[wi++] = n, Ci[wi++] = r, Ti |= r, e.lanes |= r, e = e.alternate, e !== null && (e.lanes |= r);
	}
	function Oi(e, t, n, r) {
		return Di(e, t, n, r), ji(e);
	}
	function ki(e, t) {
		return Di(e, null, null, t), ji(e);
	}
	function Ai(e, t, n) {
		e.lanes |= n;
		var r = e.alternate;
		r !== null && (r.lanes |= n);
		for (var i = !1, a = e.return; a !== null;) a.childLanes |= n, r = a.alternate, r !== null && (r.childLanes |= n), a.tag === 22 && (e = a.stateNode, e === null || e._visibility & 1 || (i = !0)), e = a, a = a.return;
		return e.tag === 3 ? (a = e.stateNode, i && t !== null && (i = 31 - tt(n), e = a.hiddenUpdates, r = e[i], r === null ? e[i] = [t] : r.push(t), t.lane = n | 536870912), a) : null;
	}
	function ji(e) {
		if (50 < kd) throw kd = 0, Ad = null, Error(i(185));
		for (var t = e.return; t !== null;) e = t, t = e.return;
		return e.tag === 3 ? e.stateNode : null;
	}
	var Mi = {};
	function Ni(e, t, n, r) {
		this.tag = e, this.key = n, this.sibling = this.child = this.return = this.stateNode = this.type = this.elementType = null, this.index = 0, this.refCleanup = this.ref = null, this.pendingProps = t, this.dependencies = this.memoizedState = this.updateQueue = this.memoizedProps = null, this.mode = r, this.subtreeFlags = this.flags = 0, this.deletions = null, this.childLanes = this.lanes = 0, this.alternate = null;
	}
	function Pi(e, t, n, r) {
		return new Ni(e, t, n, r);
	}
	function Fi(e) {
		return e = e.prototype, !(!e || !e.isReactComponent);
	}
	function Ii(e, t) {
		var n = e.alternate;
		return n === null ? (n = Pi(e.tag, t, e.key, e.mode), n.elementType = e.elementType, n.type = e.type, n.stateNode = e.stateNode, n.alternate = e, e.alternate = n) : (n.pendingProps = t, n.type = e.type, n.flags = 0, n.subtreeFlags = 0, n.deletions = null), n.flags = e.flags & 1206910976, n.childLanes = e.childLanes, n.lanes = e.lanes, n.child = e.child, n.memoizedProps = e.memoizedProps, n.memoizedState = e.memoizedState, n.updateQueue = e.updateQueue, t = e.dependencies, n.dependencies = t === null ? null : {
			lanes: t.lanes,
			firstContext: t.firstContext
		}, n.sibling = e.sibling, n.index = e.index, n.ref = e.ref, n.refCleanup = e.refCleanup, n;
	}
	function Li(e, t) {
		e.flags &= 1206910978;
		var n = e.alternate;
		return n === null ? (e.childLanes = 0, e.lanes = t, e.child = null, e.subtreeFlags = 0, e.memoizedProps = null, e.memoizedState = null, e.updateQueue = null, e.dependencies = null, e.stateNode = null) : (e.childLanes = n.childLanes, e.lanes = n.lanes, e.child = n.child, e.subtreeFlags = 0, e.deletions = null, e.memoizedProps = n.memoizedProps, e.memoizedState = n.memoizedState, e.updateQueue = n.updateQueue, e.type = n.type, t = n.dependencies, e.dependencies = t === null ? null : {
			lanes: t.lanes,
			firstContext: t.firstContext
		}), e;
	}
	function Ri(e, t, n, r, a, o) {
		var s = 0;
		if (r = e, typeof r == "function") Fi(r) && (s = 1);
		else if (typeof r == "string") s = qm(e, n, Ce.current) ? 26 : e === "html" || e === "head" || e === "body" ? 27 : 5;
		else a: switch (r) {
			case ce: return e = Pi(31, n, t, a), e.elementType = ce, e.lanes = o, e;
			case A: return zi(n.children, a, o, t);
			case te:
				s = 8, a |= 24;
				break;
			case ne: return e = Pi(12, n, t, a | 2), e.elementType = ne, e.lanes = o, e;
			case ie: return e = Pi(13, n, t, a), e.elementType = ie, e.lanes = o, e;
			case ae: return e = Pi(19, n, t, a), e.elementType = ae, e.lanes = o, e;
			case le:
			case de: return e = a | 32, e = Pi(30, n, t, e), e.elementType = de, e.lanes = o, e.stateNode = {
				autoName: null,
				paired: null,
				clones: null,
				ref: null
			}, e;
			default:
				if (typeof r == "object" && r) switch (r.$$typeof) {
					case j:
						s = 10;
						break a;
					case re:
						s = 9;
						break a;
					case M:
						s = 11;
						break a;
					case oe:
						s = 14;
						break a;
					case se:
						s = 16, r = null;
						break a;
				}
				s = 29, n = Error(i(130, e === null ? "null" : typeof e, "")), r = null;
		}
		return t = Pi(s, n, t, a), t.elementType = e, t.type = r, t.lanes = o, t;
	}
	function zi(e, t, n, r) {
		return e = Pi(7, e, r, t), e.lanes = n, e;
	}
	function Bi(e, t, n) {
		return e = Pi(6, e, null, t), e.lanes = n, e;
	}
	function Vi(e) {
		var t = Pi(18, null, null, 0);
		return t.stateNode = e, t;
	}
	function Hi(e, t, n) {
		return t = Pi(4, e.children === null ? [] : e.children, e.key, t), t.lanes = n, t.stateNode = {
			containerInfo: e.containerInfo,
			pendingChildren: null,
			implementation: e.implementation
		}, t;
	}
	var Ui = /* @__PURE__ */ new WeakMap();
	function Wi(e, t) {
		if (typeof e == "object" && e) {
			var n = Ui.get(e);
			return n === void 0 ? (t = {
				value: e,
				source: t,
				stack: Le(t)
			}, Ui.set(e, t), t) : n;
		}
		return {
			value: e,
			source: t,
			stack: Le(t)
		};
	}
	var Gi = [], Ki = 0, qi = null, Ji = 0, Yi = [], Xi = 0, Zi = null, Qi = 1, $i = "";
	function ea(e, t) {
		Gi[Ki++] = Ji, Gi[Ki++] = qi, qi = e, Ji = t;
	}
	function ta(e, t, n) {
		Yi[Xi++] = Qi, Yi[Xi++] = $i, Yi[Xi++] = Zi, Zi = e;
		var r = Qi;
		e = $i;
		var i = 32 - tt(r) - 1;
		r &= ~(1 << i), n += 1;
		var a = 32 - tt(t) + i;
		if (30 < a) {
			var o = i - i % 5;
			a = (r & (1 << o) - 1).toString(32), r >>= o, i -= o, Qi = 1 << 32 - tt(t) + i | n << i | r, $i = a + e;
		} else Qi = 1 << a | n << i | r, $i = e;
	}
	function na(e) {
		e.return !== null && (ea(e, 1), ta(e, 1, 0));
	}
	function ra(e) {
		for (; e === qi;) qi = Gi[--Ki], Gi[Ki] = null, Ji = Gi[--Ki], Gi[Ki] = null;
		for (; e === Zi;) Zi = Yi[--Xi], Yi[Xi] = null, $i = Yi[--Xi], Yi[Xi] = null, Qi = Yi[--Xi], Yi[Xi] = null;
	}
	function ia(e, t) {
		Yi[Xi++] = Qi, Yi[Xi++] = $i, Yi[Xi++] = Zi, Qi = t.id, $i = t.overflow, Zi = e;
	}
	var aa = null, R = null, z = !1, oa = null, sa = !1, ca = Error(i(519));
	function la(e) {
		throw ha(Wi(Error(i(418, 1 < arguments.length && arguments[1] !== void 0 && arguments[1] ? "text" : "HTML", "")), e)), ca;
	}
	function ua(e) {
		var t = e.stateNode, n = e.type, r = e.memoizedProps;
		switch (t[I] = e, t[Tt] = r, n) {
			case "dialog":
				Q("cancel", t), Q("close", t);
				break;
			case "iframe":
			case "object":
			case "embed":
				Q("load", t);
				break;
			case "video":
			case "audio":
				for (n = 0; n < zf.length; n++) Q(zf[n], t);
				break;
			case "source":
				Q("error", t);
				break;
			case "img":
			case "image":
			case "link":
				Q("error", t), Q("load", t);
				break;
			case "details":
				Q("toggle", t);
				break;
			case "input":
				Q("invalid", t), sn(t, r.value, r.defaultValue, r.checked, r.defaultChecked, r.type, r.name, !0);
				break;
			case "select":
				Q("invalid", t);
				break;
			case "textarea": Q("invalid", t), dn(t, r.value, r.defaultValue, r.children);
		}
		n = r.children, typeof n != "string" && typeof n != "number" && typeof n != "bigint" || t.textContent === "" + n || !0 === r.suppressHydrationWarning || ep(t.textContent, n) ? (r.popover != null && (Q("beforetoggle", t), Q("toggle", t)), r.onScroll != null && Q("scroll", t), r.onScrollEnd != null && Q("scrollend", t), r.onClick != null && (t.onclick = bn), t = !0) : t = !1, t || la(e, !0);
	}
	function da(e) {
		for (aa = e.return; aa;) switch (aa.tag) {
			case 5:
			case 31:
			case 13:
				sa = !1;
				return;
			case 27:
			case 3:
				sa = !0;
				return;
			default: aa = aa.return;
		}
	}
	function fa(e) {
		if (e !== aa) return !1;
		if (!z) return da(e), z = !0, !1;
		var t = e.tag, n;
		if ((n = t !== 3 && t !== 27) && ((n = t === 5) && (n = e.type, n = n === "form" || n === "button" || pp(e.type, e.memoizedProps)), n = !n), n && R && la(e), da(e), t === 13) {
			if (e = e.memoizedState, e = e === null ? null : e.dehydrated, !e) throw Error(i(317));
			R = dm(e);
		} else if (t === 31) {
			if (e = e.memoizedState, e = e === null ? null : e.dehydrated, !e) throw Error(i(317));
			R = dm(e);
		} else t === 27 ? (t = R, Sp(e.type) ? (e = um, um = null, R = e) : R = t) : R = aa ? lm(e.stateNode.nextSibling) : null;
		return !0;
	}
	function pa() {
		R = aa = null, z = !1;
	}
	function ma() {
		var e = oa;
		return e !== null && (pd === null ? pd = e : pd.push.apply(pd, e), oa = null), e;
	}
	function ha(e) {
		oa === null ? oa = [e] : oa.push(e);
	}
	var ga = xe(null), _a = null, va = null;
	function ya(e, t, n) {
		F(ga, t._currentValue), t._currentValue = n;
	}
	function ba(e) {
		e._currentValue = ga.current, Se(ga);
	}
	function xa(e, t, n) {
		for (; e !== null;) {
			var r = e.alternate;
			if ((e.childLanes & t) === t ? r !== null && (r.childLanes & t) !== t && (r.childLanes |= t) : (e.childLanes |= t, r !== null && (r.childLanes |= t)), e === n) break;
			e = e.return;
		}
	}
	function Sa(e, t, n, r) {
		var a = e.child;
		for (a !== null && (a.return = e); a !== null;) {
			var o = a.dependencies;
			if (o !== null) {
				var s = a.child;
				o = o.firstContext;
				a: for (; o !== null;) {
					var c = o;
					o = a;
					for (var l = 0; l < t.length; l++) if (c.context === t[l]) {
						o.lanes |= n, c = o.alternate, c !== null && (c.lanes |= n), xa(o.return, n, e), r || (s = null);
						break a;
					}
					o = c.next;
				}
			} else if (a.tag === 18) {
				if (s = a.return, s === null) throw Error(i(341));
				s.lanes |= n, o = s.alternate, o !== null && (o.lanes |= n), xa(s, n, e), s = null;
			} else a.tag === 13 && a.memoizedState !== null && a.memoizedState.dehydrated === null ? (a.lanes |= n, s = a.alternate, s !== null && (s.lanes |= n), xa(a.return, n, e), s = a.child, s = s === null ? null : s.sibling) : s = a.child;
			if (s !== null) s.return = a;
			else for (s = a; s !== null;) {
				if (s === e) {
					s = null;
					break;
				}
				if (a = s.sibling, a !== null) {
					a.return = s.return, s = a;
					break;
				}
				s = s.return;
			}
			a = s;
		}
	}
	function Ca(e, t, n, r) {
		e = null;
		for (var a = t, o = !1; a !== null;) {
			if (!o) {
				if (a.flags & 524288) o = !0;
				else if (a.flags & 262144) break;
			}
			if (a.tag === 10) {
				var s = a.alternate;
				if (s === null) throw Error(i(387));
				if (s = s.memoizedProps, s !== null) {
					var c = a.type;
					Ur(a.pendingProps.value, s.value) || (e === null ? e = [c] : e.push(c));
				}
			} else if (a === Ee.current) {
				if (s = a.alternate, s === null) throw Error(i(387));
				s.memoizedState.memoizedState !== a.memoizedState.memoizedState && (e === null ? e = [sh] : e.push(sh));
			}
			a = a.return;
		}
		return e !== null && Sa(t, e, n, r), t.flags |= 262144, e !== null;
	}
	function wa(e) {
		for (e = e.firstContext; e !== null;) {
			if (!Ur(e.context._currentValue, e.memoizedValue)) return !0;
			e = e.next;
		}
		return !1;
	}
	function Ta(e) {
		_a = e, va = null, e = e.dependencies, e !== null && (e.firstContext = null);
	}
	function Ea(e) {
		return Oa(_a, e);
	}
	function Da(e, t) {
		return _a === null && Ta(e), Oa(e, t);
	}
	function Oa(e, t) {
		var n = t._currentValue;
		if (t = {
			context: t,
			memoizedValue: n,
			next: null
		}, va === null) {
			if (e === null) throw Error(i(308));
			va = t, e.dependencies = {
				lanes: 0,
				firstContext: t
			}, e.flags |= 524288;
		} else va = va.next = t;
		return n;
	}
	var ka = typeof AbortController < "u" ? AbortController : function() {
		var e = [], t = this.signal = {
			aborted: !1,
			addEventListener: function(t, n) {
				e.push(n);
			}
		};
		this.abort = function() {
			t.aborted = !0, e.forEach(function(e) {
				return e();
			});
		};
	}, Aa = t.unstable_scheduleCallback, ja = t.unstable_NormalPriority, Ma = {
		$$typeof: j,
		Consumer: null,
		Provider: null,
		_currentValue: null,
		_currentValue2: null,
		_threadCount: 0
	};
	function Na() {
		return {
			controller: new ka(),
			data: /* @__PURE__ */ new Map(),
			refCount: 0
		};
	}
	function Pa(e) {
		e.refCount--, e.refCount === 0 && Aa(ja, function() {
			e.controller.abort();
		});
	}
	function Fa(e, t) {
		if (e.pendingLanes & 4194048) {
			var n = e.transitionTypes;
			for (n === null && (n = e.transitionTypes = []), e = 0; e < t.length; e++) {
				var r = t[e];
				n.indexOf(r) === -1 && n.push(r);
			}
		}
	}
	var Ia = null;
	function La(e) {
		var t = e.transitionTypes;
		return e.transitionTypes = null, t;
	}
	var Ra = null, za = 0, Ba = 0, Va = null;
	function Ha(e, t) {
		if (Ra === null) {
			var n = Ra = [];
			za = 0, Ba = Pf(), Va = {
				status: "pending",
				value: void 0,
				then: function(e) {
					n.push(e);
				}
			};
		}
		return za++, t.then(Ua, Ua), t;
	}
	function Ua() {
		if (--za === 0 && (Ia = null, Ra !== null)) {
			Va !== null && (Va.status = "fulfilled");
			var e = Ra;
			Ra = null, Ba = 0, Va = null;
			for (var t = 0; t < e.length; t++) (0, e[t])();
		}
	}
	function Wa(e, t) {
		var n = [], r = {
			status: "pending",
			value: null,
			reason: null,
			then: function(e) {
				n.push(e);
			}
		};
		return e.then(function() {
			r.status = "fulfilled", r.value = t;
			for (var e = 0; e < n.length; e++) (0, n[e])(t);
		}, function(e) {
			for (r.status = "rejected", r.reason = e, e = 0; e < n.length; e++) (0, n[e])(void 0);
		}), r;
	}
	var Ga = N.S;
	N.S = function(e, t) {
		if (gd = Ue(), typeof t == "object" && t && typeof t.then == "function" && Ha(e, t), Ia !== null) for (var n = bf; n !== null;) Fa(n, Ia), n = n.next;
		if (n = e.types, n !== null) {
			for (var r = bf; r !== null;) Fa(r, n), r = r.next;
			if (Ba !== 0) {
				r = Ia, r === null && (r = Ia = []);
				for (var i = 0; i < n.length; i++) {
					var a = n[i];
					r.indexOf(a) === -1 && r.push(a);
				}
			}
		}
		Ga !== null && Ga(e, t);
	};
	var Ka = xe(null);
	function qa() {
		var e = Ka.current;
		return e === null ? K.pooledCache : e;
	}
	function Ja(e, t) {
		t === null ? F(Ka, Ka.current) : F(Ka, t.pool);
	}
	function Ya() {
		var e = qa();
		return e === null ? null : {
			parent: Ma._currentValue,
			pool: e
		};
	}
	var Xa = Error(i(460)), Za = Error(i(474)), Qa = Error(i(542)), $a = { then: function() {} };
	function eo(e) {
		return e = e.status, e === "fulfilled" || e === "rejected";
	}
	function to(e, t, n) {
		switch (n = e[n], n === void 0 ? e.push(t) : n !== t && (t.then(bn, bn), t = n), t.status) {
			case "fulfilled": return t.value;
			case "rejected": throw e = t.reason, ao(e), e === void 0 && !("reason" in t) ? Error(i(600)) : e;
			default:
				if (typeof t.status == "string") t.then(bn, bn);
				else {
					if (e = K, e !== null && 100 < e.shellSuspendCounter) throw Error(i(482));
					e = t, e.status = "pending", e.then(function(e) {
						if (t.status === "pending") {
							var n = t;
							n.status = "fulfilled", n.value = e;
						}
					}, function(e) {
						if (t.status === "pending") {
							var n = t;
							n.status = "rejected", n.reason = e;
						}
					});
				}
				switch (t.status) {
					case "fulfilled": return t.value;
					case "rejected": throw e = t.reason, ao(e), e;
				}
				throw ro = t, Xa;
		}
	}
	function no(e) {
		try {
			var t = e._init;
			return t(e._payload);
		} catch (e) {
			throw typeof e == "object" && e && typeof e.then == "function" ? (ro = e, Xa) : e;
		}
	}
	var ro = null;
	function io() {
		if (ro === null) throw Error(i(459));
		var e = ro;
		return ro = null, e;
	}
	function ao(e) {
		if (e === Xa || e === Qa) throw Error(i(483));
	}
	var oo = null, so = 0;
	function co(e) {
		var t = so;
		return so += 1, oo === null && (oo = []), to(oo, e, t);
	}
	function lo(e, t) {
		t = t.props.ref, e.ref = t === void 0 ? null : t;
	}
	function uo(e, t) {
		throw t.$$typeof === O ? Error(i(525)) : (e = Object.prototype.toString.call(t), Error(i(31, e === "[object Object]" ? "object with keys {" + Object.keys(t).join(", ") + "}" : e)));
	}
	function fo(e) {
		function t(t, n) {
			if (e) {
				var r = t.deletions;
				r === null ? (t.deletions = [n], t.flags |= 16) : r.push(n);
			}
		}
		function n(n, r) {
			if (!e) return null;
			for (; r !== null;) t(n, r), r = r.sibling;
			return null;
		}
		function r(e) {
			for (var t = /* @__PURE__ */ new Map(); e !== null;) e.key === null ? t.set(e.index, e) : t.set(e.key, e), e = e.sibling;
			return t;
		}
		function a(e, t) {
			return e = Ii(e, t), e.index = 0, e.sibling = null, e;
		}
		function o(t, n, r) {
			return t.index = r, e ? (r = t.alternate, r === null ? (t.flags |= 134217730, n) : (r = r.index, r < n ? (t.flags |= 2, n) : r)) : (t.flags |= 1048576, n);
		}
		function s(t) {
			return e && t.alternate === null && (t.flags |= 134217730), t;
		}
		function c(e, t, n, r) {
			return t === null || t.tag !== 6 ? (t = Bi(n, e.mode, r), t.return = e, t) : (t = a(t, n), t.return = e, t);
		}
		function l(e, t, n, r) {
			var i = n.type;
			return i === A ? (e = d(e, t, n.props.children, r, n.key), lo(e, n), e) : t !== null && (t.elementType === i || typeof i == "object" && i && i.$$typeof === se && no(i) === t.type) ? (t = a(t, n.props), lo(t, n), t.return = e, t) : (t = Ri(n.type, n.key, n.props, null, e.mode, r), lo(t, n), t.return = e, t);
		}
		function u(e, t, n, r) {
			return t === null || t.tag !== 4 || t.stateNode.containerInfo !== n.containerInfo || t.stateNode.implementation !== n.implementation ? (t = Hi(n, e.mode, r), t.return = e, t) : (t = a(t, n.children || []), t.return = e, t);
		}
		function d(e, t, n, r, i) {
			return t === null || t.tag !== 7 ? (t = zi(n, e.mode, r, i), t.return = e, t) : (t = a(t, n), t.return = e, t);
		}
		function f(e, t, n) {
			if (typeof t == "string" && t !== "" || typeof t == "number" || typeof t == "bigint") return t = Bi("" + t, e.mode, n), t.return = e, t;
			if (typeof t == "object" && t) {
				switch (t.$$typeof) {
					case k: return n = Ri(t.type, t.key, t.props, null, e.mode, n), lo(n, t), n.return = e, n;
					case ee: return t = Hi(t, e.mode, n), t.return = e, t;
					case se: return t = no(t), f(e, t, n);
				}
				if (_e(t) || me(t)) return t = zi(t, e.mode, n, null), t.return = e, t;
				if (typeof t.then == "function") return f(e, co(t), n);
				if (t.$$typeof === j) return f(e, Da(e, t), n);
				uo(e, t);
			}
			return null;
		}
		function p(e, t, n, r) {
			var i = t === null ? null : t.key;
			if (typeof n == "string" && n !== "" || typeof n == "number" || typeof n == "bigint") return i === null ? c(e, t, "" + n, r) : null;
			if (typeof n == "object" && n) {
				switch (n.$$typeof) {
					case k: return n.key === i ? l(e, t, n, r) : null;
					case ee: return n.key === i ? u(e, t, n, r) : null;
					case se: return n = no(n), p(e, t, n, r);
				}
				if (_e(n) || me(n)) return i === null ? d(e, t, n, r, null) : null;
				if (typeof n.then == "function") return p(e, t, co(n), r);
				if (n.$$typeof === j) return p(e, t, Da(e, n), r);
				uo(e, n);
			}
			return null;
		}
		function m(e, t, n, r, i) {
			if (typeof r == "string" && r !== "" || typeof r == "number" || typeof r == "bigint") return e = e.get(n) || null, c(t, e, "" + r, i);
			if (typeof r == "object" && r) {
				switch (r.$$typeof) {
					case k: return e = e.get(r.key === null ? n : r.key) || null, l(t, e, r, i);
					case ee: return e = e.get(r.key === null ? n : r.key) || null, u(t, e, r, i);
					case se: return r = no(r), m(e, t, n, r, i);
				}
				if (_e(r) || me(r)) return e = e.get(n) || null, d(t, e, r, i, null);
				if (typeof r.then == "function") return m(e, t, n, co(r), i);
				if (r.$$typeof === j) return m(e, t, n, Da(t, r), i);
				uo(t, r);
			}
			return null;
		}
		function h(i, a, s, c) {
			for (var l = null, u = null, d = a, h = a = 0, g = null; d !== null && h < s.length; h++) {
				d.index > h ? (g = d, d = null) : g = d.sibling;
				var _ = p(i, d, s[h], c);
				if (_ === null) {
					d === null && (d = g);
					break;
				}
				e && d && _.alternate === null && t(i, d), a = o(_, a, h), u === null ? l = _ : u.sibling = _, u = _, d = g;
			}
			if (h === s.length) return n(i, d), z && ea(i, h), l;
			if (d === null) {
				for (; h < s.length; h++) d = f(i, s[h], c), d !== null && (a = o(d, a, h), u === null ? l = d : u.sibling = d, u = d);
				return z && ea(i, h), l;
			}
			for (d = r(d); h < s.length; h++) g = m(d, i, h, s[h], c), g !== null && (e && (_ = g.alternate, _ !== null && d.delete(_.key === null ? h : _.key)), a = o(g, a, h), u === null ? l = g : u.sibling = g, u = g);
			return e && d.forEach(function(e) {
				return t(i, e);
			}), z && ea(i, h), l;
		}
		function g(a, s, c, l) {
			if (c == null) throw Error(i(151));
			for (var u = null, d = null, h = s, g = s = 0, _ = null, v = c.next(); h !== null && !v.done; g++, v = c.next()) {
				h.index > g ? (_ = h, h = null) : _ = h.sibling;
				var y = p(a, h, v.value, l);
				if (y === null) {
					h === null && (h = _);
					break;
				}
				e && h && y.alternate === null && t(a, h), s = o(y, s, g), d === null ? u = y : d.sibling = y, d = y, h = _;
			}
			if (v.done) return n(a, h), z && ea(a, g), u;
			if (h === null) {
				for (; !v.done; g++, v = c.next()) v = f(a, v.value, l), v !== null && (s = o(v, s, g), d === null ? u = v : d.sibling = v, d = v);
				return z && ea(a, g), u;
			}
			for (h = r(h); !v.done; g++, v = c.next()) v = m(h, a, g, v.value, l), v !== null && (e && (_ = v.alternate, _ !== null && h.delete(_.key === null ? g : _.key)), s = o(v, s, g), d === null ? u = v : d.sibling = v, d = v);
			return e && h.forEach(function(e) {
				return t(a, e);
			}), z && ea(a, g), u;
		}
		function _(e, r, o, c) {
			if (typeof o == "object" && o && o.type === A && o.key === null && o.props.ref === void 0 && (o = o.props.children), typeof o == "object" && o) {
				switch (o.$$typeof) {
					case k:
						a: {
							for (var l = o.key; r !== null;) {
								if (r.key === l) {
									if (l = o.type, l === A) {
										if (r.tag === 7) {
											n(e, r.sibling), c = a(r, o.props.children), lo(c, o), c.return = e, e = c;
											break a;
										}
									} else if (r.elementType === l || typeof l == "object" && l && l.$$typeof === se && no(l) === r.type) {
										n(e, r.sibling), c = a(r, o.props), lo(c, o), c.return = e, e = c;
										break a;
									}
									n(e, r);
									break;
								}
								t(e, r), r = r.sibling;
							}
							o.type === A ? (c = zi(o.props.children, e.mode, c, o.key), lo(c, o), c.return = e, e = c) : (c = Ri(o.type, o.key, o.props, null, e.mode, c), lo(c, o), c.return = e, e = c);
						}
						return s(e);
					case ee:
						a: {
							for (l = o.key; r !== null;) {
								if (r.key === l) {
									if (r.tag === 4 && r.stateNode.containerInfo === o.containerInfo && r.stateNode.implementation === o.implementation) {
										n(e, r.sibling), c = a(r, o.children || []), c.return = e, e = c;
										break a;
									}
									n(e, r);
									break;
								}
								t(e, r), r = r.sibling;
							}
							c = Hi(o, e.mode, c), c.return = e, e = c;
						}
						return s(e);
					case se: return o = no(o), _(e, r, o, c);
				}
				if (_e(o)) return h(e, r, o, c);
				if (me(o)) {
					if (l = me(o), typeof l != "function") throw Error(i(150));
					return o = l.call(o), g(e, r, o, c);
				}
				if (typeof o.then == "function") return _(e, r, co(o), c);
				if (o.$$typeof === j) return _(e, r, Da(e, o), c);
				uo(e, o);
			}
			return typeof o == "string" && o !== "" || typeof o == "number" || typeof o == "bigint" ? (o = "" + o, r !== null && r.tag === 6 ? (n(e, r.sibling), c = a(r, o), c.return = e, e = c) : (n(e, r), c = Bi(o, e.mode, c), c.return = e, e = c), s(e)) : n(e, r);
		}
		return function(e, t, n, r) {
			try {
				so = 0;
				var i = _(e, t, n, r);
				return oo = null, i;
			} catch (t) {
				if (t === Xa || t === Qa) throw t;
				var a = Pi(29, t, null, e.mode);
				return a.lanes = r, a.return = e, a;
			}
		};
	}
	var po = fo(!0), mo = fo(!1), ho = !1;
	function go(e) {
		e.updateQueue = {
			baseState: e.memoizedState,
			firstBaseUpdate: null,
			lastBaseUpdate: null,
			shared: {
				pending: null,
				lanes: 0,
				hiddenCallbacks: null
			},
			callbacks: null
		};
	}
	function _o(e, t) {
		e = e.updateQueue, t.updateQueue === e && (t.updateQueue = {
			baseState: e.baseState,
			firstBaseUpdate: e.firstBaseUpdate,
			lastBaseUpdate: e.lastBaseUpdate,
			shared: e.shared,
			callbacks: null
		});
	}
	function vo(e) {
		return {
			lane: e,
			tag: 0,
			payload: null,
			callback: null,
			next: null
		};
	}
	function yo(e, t, n) {
		var r = e.updateQueue;
		if (r === null) return null;
		if (r = r.shared, G & 2) {
			var i = r.pending;
			return i === null ? t.next = t : (t.next = i.next, i.next = t), r.pending = t, t = ji(e), Ai(e, null, n), t;
		}
		return Di(e, r, t, n), ji(e);
	}
	function bo(e, t, n) {
		if (t = t.updateQueue, t !== null && (t = t.shared, n & 4194048)) {
			var r = t.lanes;
			r &= e.pendingLanes, n |= r, t.lanes = n, vt(e, n);
		}
	}
	function xo(e, t) {
		var n = e.updateQueue, r = e.alternate;
		if (r !== null && (r = r.updateQueue, n === r)) {
			var i = null, a = null;
			if (n = n.firstBaseUpdate, n !== null) {
				do {
					var o = {
						lane: n.lane,
						tag: n.tag,
						payload: n.payload,
						callback: null,
						next: null
					};
					a === null ? i = a = o : a = a.next = o, n = n.next;
				} while (n !== null);
				a === null ? i = a = t : a = a.next = t;
			} else i = a = t;
			n = {
				baseState: r.baseState,
				firstBaseUpdate: i,
				lastBaseUpdate: a,
				shared: r.shared,
				callbacks: r.callbacks
			}, e.updateQueue = n;
			return;
		}
		e = n.lastBaseUpdate, e === null ? n.firstBaseUpdate = t : e.next = t, n.lastBaseUpdate = t;
	}
	var So = !1;
	function Co() {
		if (So) {
			var e = Va;
			if (e !== null) throw e;
		}
	}
	function wo(e, t, n, r) {
		So = !1;
		var i = e.updateQueue;
		ho = !1;
		var a = i.firstBaseUpdate, o = i.lastBaseUpdate, s = i.shared.pending;
		if (s !== null) {
			i.shared.pending = null;
			var c = s, l = c.next;
			c.next = null, o === null ? a = l : o.next = l, o = c;
			var u = e.alternate;
			u !== null && (u = u.updateQueue, s = u.lastBaseUpdate, s !== o && (s === null ? u.firstBaseUpdate = l : s.next = l, u.lastBaseUpdate = c));
		}
		if (a !== null) {
			var d = i.baseState;
			o = 0, u = l = c = null, s = a;
			do {
				var f = s.lane & -536870913, p = f !== s.lane;
				if (p ? (J & f) === f : (r & f) === f) {
					f !== 0 && f === Ba && (So = !0), u !== null && (u = u.next = {
						lane: 0,
						tag: s.tag,
						payload: s.payload,
						callback: null,
						next: null
					});
					a: {
						var m = e, h = s;
						f = t;
						var g = n;
						switch (h.tag) {
							case 1:
								if (m = h.payload, typeof m == "function") {
									d = m.call(g, d, f);
									break a;
								}
								d = m;
								break a;
							case 3: m.flags = m.flags & -65537 | 128;
							case 0:
								if (m = h.payload, f = typeof m == "function" ? m.call(g, d, f) : m, f == null) break a;
								d = D({}, d, f);
								break a;
							case 2: ho = !0;
						}
					}
					f = s.callback, f !== null && (e.flags |= 64, p && (e.flags |= 8192), p = i.callbacks, p === null ? i.callbacks = [f] : p.push(f));
				} else p = {
					lane: f,
					tag: s.tag,
					payload: s.payload,
					callback: s.callback,
					next: null
				}, u === null ? (l = u = p, c = d) : u = u.next = p, o |= f;
				if (s = s.next, s === null) {
					if (s = i.shared.pending, s === null) break;
					p = s, s = p.next, p.next = null, i.lastBaseUpdate = p, i.shared.pending = null;
				}
			} while (1);
			u === null && (c = d), i.baseState = c, i.firstBaseUpdate = l, i.lastBaseUpdate = u, a === null && (i.shared.lanes = 0), sd |= o, e.lanes = o, e.memoizedState = d;
		}
	}
	function To(e, t) {
		if (typeof e != "function") throw Error(i(191, e));
		e.call(t);
	}
	function Eo(e, t) {
		var n = e.callbacks;
		if (n !== null) for (e.callbacks = null, e = 0; e < n.length; e++) To(n[e], t);
	}
	var Do = xe(null), Oo = xe(0);
	function ko(e, t) {
		e = ad, F(Oo, e), F(Do, t), ad = e | t.baseLanes;
	}
	function Ao() {
		F(Oo, ad), F(Do, Do.current);
	}
	function jo() {
		ad = Oo.current, Se(Do), Se(Oo);
	}
	var Mo = xe(null), No = null;
	function Po(e) {
		var t = e.alternate;
		F(zo, zo.current & 1), F(Mo, e), No === null && (t === null || Do.current !== null || t.memoizedState !== null) && (No = e);
	}
	function Fo(e) {
		F(zo, zo.current), F(Mo, e), No === null && (No = e);
	}
	function Io(e) {
		e.tag === 22 ? (F(zo, zo.current), F(Mo, e), No === null && (No = e)) : Lo();
	}
	function Lo() {
		F(zo, zo.current), F(Mo, Mo.current);
	}
	function Ro(e) {
		Se(Mo), No === e && (No = null), Se(zo);
	}
	var zo = xe(0);
	function Bo(e, t) {
		F(Mo, Mo.current), F(zo, t);
	}
	function Vo(e) {
		Se(zo), Se(Mo), No === e && (No = null);
	}
	function Ho(e) {
		for (var t = e; t !== null;) {
			if (t.tag === 13) {
				var n = t.memoizedState;
				if (n !== null && (n = n.dehydrated, n === null || om(n) || sm(n))) return t;
			} else if (t.tag === 19 && t.memoizedProps.revealOrder !== "independent") {
				if (t.flags & 128) return t;
			} else if (t.child !== null) {
				t.child.return = t, t = t.child;
				continue;
			}
			if (t === e) break;
			for (; t.sibling === null;) {
				if (t.return === null || t.return === e) return null;
				t = t.return;
			}
			t.sibling.return = t.return, t = t.sibling;
		}
		return null;
	}
	var Uo = 0, B = null, V = null, Wo = null, Go = !1, Ko = !1, qo = !1, Jo = 0, Yo = 0, Xo = null, Zo = 0;
	function Qo() {
		throw Error(i(321));
	}
	function $o(e, t) {
		if (t === null) return !1;
		for (var n = 0; n < t.length && n < e.length; n++) if (!Ur(e[n], t[n])) return !1;
		return !0;
	}
	function es(e, t, n, r, i, a) {
		return Uo = a, B = t, t.memoizedState = null, t.updateQueue = null, t.lanes = 0, N.H = e === null || e.memoizedState === null ? _c : vc, qo = !1, a = n(r, i), qo = !1, Ko && (a = ns(t, n, r, i)), ts(e), a;
	}
	function ts(e) {
		N.H = gc;
		var t = V !== null && V.next !== null;
		if (Uo = 0, Wo = V = B = null, Go = !1, Yo = 0, Xo = null, t) throw Error(i(300));
		e === null || Fc || (e = e.dependencies, e !== null && wa(e) && (Fc = !0));
	}
	function ns(e, t, n, r) {
		B = e;
		var a = 0;
		do {
			if (Ko && (Xo = null), Yo = 0, Ko = !1, 25 <= a) throw Error(i(301));
			if (a += 1, Wo = V = null, e.updateQueue != null) {
				var o = e.updateQueue;
				o.lastEffect = null, o.events = null, o.stores = null, o.memoCache != null && (o.memoCache.index = 0);
			}
			N.H = yc, o = t(n, r);
		} while (Ko);
		return o;
	}
	function rs() {
		var e = N.H, t = e.useState()[0];
		return t = typeof t.then == "function" ? us(t) : t, e = e.useState()[0], (V === null ? null : V.memoizedState) !== e && (B.flags |= 1024), t;
	}
	function is() {
		var e = Jo !== 0;
		return Jo = 0, e;
	}
	function as(e, t, n) {
		t.updateQueue = e.updateQueue, t.flags &= -2053, e.lanes &= ~n;
	}
	function os(e) {
		if (Go) {
			for (e = e.memoizedState; e !== null;) {
				var t = e.queue;
				t !== null && (t.pending = null), e = e.next;
			}
			Go = !1;
		}
		Uo = 0, Wo = V = B = null, Ko = !1, Yo = Jo = 0, Xo = null;
	}
	function ss() {
		var e = {
			memoizedState: null,
			baseState: null,
			baseQueue: null,
			queue: null,
			next: null
		};
		return Wo === null ? B.memoizedState = Wo = e : Wo = Wo.next = e, Wo;
	}
	function cs() {
		if (V === null) {
			var e = B.alternate;
			e = e === null ? null : e.memoizedState;
		} else e = V.next;
		var t = Wo === null ? B.memoizedState : Wo.next;
		if (t !== null) Wo = t, V = e;
		else {
			if (e === null) throw B.alternate === null ? Error(i(467)) : Error(i(310));
			V = e, e = {
				memoizedState: V.memoizedState,
				baseState: V.baseState,
				baseQueue: V.baseQueue,
				queue: V.queue,
				next: null
			}, Wo === null ? B.memoizedState = Wo = e : Wo = Wo.next = e;
		}
		return Wo;
	}
	function ls() {
		return {
			lastEffect: null,
			events: null,
			stores: null,
			memoCache: null
		};
	}
	function us(e) {
		var t = Yo;
		return Yo += 1, Xo === null && (Xo = []), e = to(Xo, e, t), t = B, (Wo === null ? t.memoizedState : Wo.next) === null && (t = t.alternate, N.H = t === null || t.memoizedState === null ? _c : vc), e;
	}
	function ds(e) {
		if (typeof e == "object" && e) {
			if (typeof e.then == "function") return us(e);
			if (e.$$typeof === fe) return;
			if (e.$$typeof === j) return Ea(e);
		}
		throw Error(i(438, String(e)));
	}
	function fs(e) {
		var t = null, n = B.updateQueue;
		if (n !== null && (t = n.memoCache), t == null) {
			var r = B.alternate;
			r !== null && (r = r.updateQueue, r !== null && (r = r.memoCache, r != null && (t = {
				data: r.data.map(function(e) {
					return e.slice();
				}),
				index: 0
			})));
		}
		if (t ??= {
			data: [],
			index: 0
		}, n === null && (n = ls(), B.updateQueue = n), n.memoCache = t, n = t.data[t.index], n === void 0) for (n = t.data[t.index] = Array(e), r = 0; r < e; r++) n[r] = ue;
		return t.index++, n;
	}
	function ps(e, t) {
		return typeof t == "function" ? t(e) : t;
	}
	function ms(e) {
		return hs(cs(), V, e);
	}
	function hs(e, t, n) {
		var r = e.queue;
		if (r === null) throw Error(i(311));
		r.lastRenderedReducer = n;
		var a = e.baseQueue, o = r.pending;
		if (o !== null) {
			if (a !== null) {
				var s = a.next;
				a.next = o.next, o.next = s;
			}
			t.baseQueue = a = o, r.pending = null;
		}
		if (o = e.baseState, a === null) e.memoizedState = o;
		else {
			t = a.next;
			var c = s = null, l = null, u = t, d = !1;
			do {
				var f = u.lane & -536870913;
				if (f === u.lane ? (Uo & f) === f : (J & f) === f) {
					var p = u.revertLane;
					if (p === 0) l !== null && (l = l.next = {
						lane: 0,
						revertLane: 0,
						gesture: null,
						action: u.action,
						hasEagerState: u.hasEagerState,
						eagerState: u.eagerState,
						next: null
					}), f === Ba && (d = !0);
					else if ((Uo & p) === p) {
						u = u.next, p === Ba && (d = !0);
						continue;
					} else f = {
						lane: 0,
						revertLane: u.revertLane,
						gesture: null,
						action: u.action,
						hasEagerState: u.hasEagerState,
						eagerState: u.eagerState,
						next: null
					}, l === null ? (c = l = f, s = o) : l = l.next = f, B.lanes |= p, sd |= p;
					f = u.action, qo && n(o, f), o = u.hasEagerState ? u.eagerState : n(o, f);
				} else p = {
					lane: f,
					revertLane: u.revertLane,
					gesture: u.gesture,
					action: u.action,
					hasEagerState: u.hasEagerState,
					eagerState: u.eagerState,
					next: null
				}, l === null ? (c = l = p, s = o) : l = l.next = p, B.lanes |= f, sd |= f;
				u = u.next;
			} while (u !== null && u !== t);
			if (l === null ? s = o : l.next = c, !Ur(o, e.memoizedState) && (Fc = !0, d && (n = Va, n !== null))) throw n;
			e.memoizedState = o, e.baseState = s, e.baseQueue = l, r.lastRenderedState = o;
		}
		return a === null && (r.lanes = 0), [e.memoizedState, r.dispatch];
	}
	function gs(e) {
		var t = cs(), n = t.queue;
		if (n === null) throw Error(i(311));
		n.lastRenderedReducer = e;
		var r = n.dispatch, a = n.pending, o = t.memoizedState;
		if (a !== null) {
			n.pending = null;
			var s = a = a.next;
			do
				o = e(o, s.action), s = s.next;
			while (s !== a);
			Ur(o, t.memoizedState) || (Fc = !0), t.memoizedState = o, t.baseQueue === null && (t.baseState = o), n.lastRenderedState = o;
		}
		return [o, r];
	}
	function _s(e, t, n) {
		var r = B, a = cs(), o = z;
		if (o) {
			if (n === void 0) throw Error(i(407));
			n = n();
		} else n = t();
		var s = !Ur((V || a).memoizedState, n);
		if (s && (a.memoizedState = n, Fc = !0), a = a.queue, Hs(bs.bind(null, r, a, e), [e]), e = a.getSnapshot !== t || s || Wo !== null && !!(Wo.memoizedState.tag & 1), Ls(e ? 9 : 8, { destroy: void 0 }, ys.bind(null, r, a, n, t), null), e) {
			if (r.flags |= 2048, K === null) throw Error(i(349));
			o || Uo & 127 || vs(r, t, n);
		}
		return n;
	}
	function vs(e, t, n) {
		e.flags |= 16384, e = {
			getSnapshot: t,
			value: n
		}, t = B.updateQueue, t === null ? (t = ls(), B.updateQueue = t, t.stores = [e]) : (n = t.stores, n === null ? t.stores = [e] : n.push(e));
	}
	function ys(e, t, n, r) {
		t.value = n, t.getSnapshot = r, xs(t) && Ss(e);
	}
	function bs(e, t, n) {
		return n(function() {
			xs(t) && Ss(e);
		});
	}
	function xs(e) {
		var t = e.getSnapshot;
		e = e.value;
		try {
			var n = t();
			return !Ur(e, n);
		} catch {
			return !0;
		}
	}
	function Ss(e) {
		var t = ki(e, 2);
		t !== null && Pd(t, e, 2);
	}
	function Cs(e) {
		var t = ss();
		if (typeof e == "function") {
			var n = e;
			if (e = n(), qo) {
				et(!0);
				try {
					n();
				} finally {
					et(!1);
				}
			}
		}
		return t.memoizedState = t.baseState = e, t.queue = {
			pending: null,
			lanes: 0,
			dispatch: null,
			lastRenderedReducer: ps,
			lastRenderedState: e
		}, t;
	}
	function ws(e, t, n, r) {
		return e.baseState = n, hs(e, V, typeof r == "function" ? r : ps);
	}
	function Ts(e, t, n, r, a) {
		if (pc(e)) throw Error(i(485));
		if (e = t.action, e !== null) {
			var o = {
				payload: a,
				action: e,
				next: null,
				isTransition: !0,
				status: "pending",
				value: null,
				reason: null,
				listeners: [],
				then: function(e) {
					o.listeners.push(e);
				}
			};
			N.T === null ? o.isTransition = !1 : n(!0), r(o), n = t.pending, n === null ? (o.next = t.pending = o, Es(t, o)) : (o.next = n.next, t.pending = n.next = o);
		}
	}
	function Es(e, t) {
		var n = t.action, r = t.payload, i = e.state;
		if (t.isTransition) {
			var a = N.T, o = {};
			o.types = a === null ? null : a.types, N.T = o;
			try {
				var s = n(i, r), c = N.S;
				c !== null && c(o, s), Ds(e, t, s);
			} catch (n) {
				ks(e, t, n);
			} finally {
				a !== null && o.types !== null && (a.types = o.types), N.T = a;
			}
		} else try {
			a = n(i, r), Ds(e, t, a);
		} catch (n) {
			ks(e, t, n);
		}
	}
	function Ds(e, t, n) {
		typeof n == "object" && n && typeof n.then == "function" ? n.then(function(n) {
			Os(e, t, n);
		}, function(n) {
			return ks(e, t, n);
		}) : Os(e, t, n);
	}
	function Os(e, t, n) {
		t.status = "fulfilled", t.value = n, As(t), e.state = n, t = e.pending, t !== null && (n = t.next, n === t ? e.pending = null : (n = n.next, t.next = n, Es(e, n)));
	}
	function ks(e, t, n) {
		var r = e.pending;
		if (e.pending = null, r !== null) {
			r = r.next;
			do
				t.status = "rejected", t.reason = n, As(t), t = t.next;
			while (t !== r);
		}
		e.action = null;
	}
	function As(e) {
		e = e.listeners;
		for (var t = 0; t < e.length; t++) (0, e[t])();
	}
	function js(e, t) {
		return t;
	}
	function Ms(e, t) {
		if (z) {
			var n = K.formState;
			if (n !== null) {
				a: {
					var r = B;
					if (z) {
						if (R) {
							b: {
								for (var i = R, a = sa; i.nodeType !== 8;) {
									if (!a) {
										i = null;
										break b;
									}
									if (i = lm(i.nextSibling), i === null) {
										i = null;
										break b;
									}
								}
								a = i.data, i = a === "F!" || a === "F" ? i : null;
							}
							if (i) {
								R = lm(i.nextSibling), r = i.data === "F!";
								break a;
							}
						}
						la(r);
					}
					r = !1;
				}
				r && (t = n[0]);
			}
		}
		return n = ss(), n.memoizedState = n.baseState = t, r = {
			pending: null,
			lanes: 0,
			dispatch: null,
			lastRenderedReducer: js,
			lastRenderedState: t
		}, n.queue = r, n = uc.bind(null, B, r), r.dispatch = n, r = Cs(!1), a = fc.bind(null, B, !1, r.queue), r = ss(), i = {
			state: t,
			dispatch: null,
			action: e,
			pending: null
		}, r.queue = i, n = Ts.bind(null, B, i, a, n), i.dispatch = n, r.memoizedState = e, [
			t,
			n,
			!1
		];
	}
	function Ns(e) {
		return Ps(cs(), V, e);
	}
	function Ps(e, t, n) {
		if (t = hs(e, t, js)[0], e = ms(ps)[0], typeof t == "object" && t && typeof t.then == "function") try {
			var r = us(t);
		} catch (e) {
			throw e === Xa ? Qa : e;
		}
		else r = t;
		t = cs();
		var i = t.queue, a = i.dispatch;
		return n !== t.memoizedState && (B.flags |= 2048, Ls(9, { destroy: void 0 }, Fs.bind(null, i, n), null)), [
			r,
			a,
			e
		];
	}
	function Fs(e, t) {
		e.action = t;
	}
	function Is(e) {
		var t = cs(), n = V;
		if (n !== null) return Ps(t, n, e);
		cs(), t = t.memoizedState, n = cs();
		var r = n.queue.dispatch;
		return n.memoizedState = e, [
			t,
			r,
			!1
		];
	}
	function Ls(e, t, n, r) {
		return e = {
			tag: e,
			create: n,
			deps: r,
			inst: t,
			next: null
		}, t = B.updateQueue, t === null && (t = ls(), B.updateQueue = t), n = t.lastEffect, n === null ? t.lastEffect = e.next = e : (r = n.next, n.next = e, e.next = r, t.lastEffect = e), e;
	}
	function Rs() {
		return cs().memoizedState;
	}
	function zs(e, t, n, r) {
		var i = ss();
		B.flags |= e, i.memoizedState = Ls(1 | t, { destroy: void 0 }, n, r === void 0 ? null : r);
	}
	function Bs(e, t, n, r) {
		var i = cs();
		r = r === void 0 ? null : r;
		var a = i.memoizedState.inst;
		V !== null && r !== null && $o(r, V.memoizedState.deps) ? i.memoizedState = Ls(t, a, n, r) : (B.flags |= e, i.memoizedState = Ls(1 | t, a, n, r));
	}
	function Vs(e, t) {
		zs(8390656, 8, e, t);
	}
	function Hs(e, t) {
		Bs(2048, 8, e, t);
	}
	function Us(e) {
		B.flags |= 4;
		var t = B.updateQueue;
		if (t === null) t = ls(), B.updateQueue = t, t.events = [e];
		else {
			var n = t.events;
			n === null ? t.events = [e] : n.push(e);
		}
	}
	function Ws(e) {
		var t = cs().memoizedState;
		return Us({
			ref: t,
			nextImpl: e
		}), function() {
			if (G & 2) throw Error(i(440));
			return t.impl.apply(void 0, arguments);
		};
	}
	function Gs(e, t) {
		return Bs(4, 2, e, t);
	}
	function Ks(e, t) {
		return Bs(4, 4, e, t);
	}
	function qs(e, t) {
		if (typeof t == "function") {
			e = e();
			var n = t(e);
			return function() {
				typeof n == "function" ? n() : t(null);
			};
		}
		if (t != null) return e = e(), t.current = e, function() {
			t.current = null;
		};
	}
	function Js(e, t, n) {
		n = n == null ? null : n.concat([e]), Bs(4, 4, qs.bind(null, t, e), n);
	}
	function Ys() {}
	function Xs(e, t) {
		var n = cs();
		t = t === void 0 ? null : t;
		var r = n.memoizedState;
		return t !== null && $o(t, r[1]) ? r[0] : (n.memoizedState = [e, t], e);
	}
	function Zs(e, t) {
		var n = cs();
		t = t === void 0 ? null : t;
		var r = n.memoizedState;
		if (t !== null && $o(t, r[1])) return r[0];
		if (r = e(), qo) {
			et(!0);
			try {
				e();
			} finally {
				et(!1);
			}
		}
		return n.memoizedState = [r, t], r;
	}
	function Qs(e, t, n) {
		return n === void 0 || Uo & 1073741824 && !(J & 261930) ? e.memoizedState = t : (e.memoizedState = n, e = Md(), B.lanes |= e, sd |= e, n);
	}
	function $s(e, t, n, r) {
		return Ur(n, t) ? n : Do.current === null ? !(Uo & 106) || Uo & 1073741824 && !(J & 261930) ? (Fc = !0, e.memoizedState = n) : (e = Md(), B.lanes |= e, sd |= e, t) : (e = Qs(e, n, r), Ur(e, t) || (Fc = !0), e);
	}
	function ec(e, t, n, r, i) {
		var a = P.p;
		P.p = a !== 0 && 8 > a ? a : 8;
		var o = N.T, s = {};
		s.types = o === null ? null : o.types, N.T = s, fc(e, !1, t, n);
		try {
			var c = i(), l = N.S;
			l !== null && l(s, c), typeof c == "object" && c && typeof c.then == "function" ? dc(e, t, Wa(c, r), jd(e)) : dc(e, t, r, jd(e));
		} catch (n) {
			dc(e, t, {
				then: function() {},
				status: "rejected",
				reason: n
			}, jd());
		} finally {
			P.p = a, o !== null && s.types !== null && (o.types = s.types), N.T = o;
		}
	}
	function tc() {}
	function nc(e, t, n, r) {
		if (e.tag !== 5) throw Error(i(476));
		var a = rc(e).queue;
		ec(e, a, t, ve, n === null ? tc : function() {
			return ic(e), n(r);
		});
	}
	function rc(e) {
		var t = e.memoizedState;
		if (t !== null) return t;
		t = {
			memoizedState: ve,
			baseState: ve,
			baseQueue: null,
			queue: {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: ps,
				lastRenderedState: ve
			},
			next: null
		};
		var n = {};
		return t.next = {
			memoizedState: n,
			baseState: n,
			baseQueue: null,
			queue: {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: ps,
				lastRenderedState: n
			},
			next: null
		}, e.memoizedState = t, e = e.alternate, e !== null && (e.memoizedState = t), t;
	}
	function ic(e) {
		var t = rc(e);
		t.next === null && (t = e.alternate.memoizedState), dc(e, t.next.queue, {}, jd());
	}
	function ac() {
		return Ea(sh);
	}
	function oc() {
		return cs().memoizedState;
	}
	function sc() {
		return cs().memoizedState;
	}
	function cc(e) {
		for (var t = e.return; t !== null;) {
			switch (t.tag) {
				case 24:
				case 3:
					var n = jd();
					e = vo(n);
					var r = yo(t, e, n);
					r !== null && (Pd(r, t, n), bo(r, t, n)), t = { cache: Na() }, e.payload = t;
					return;
			}
			t = t.return;
		}
	}
	function lc(e, t, n) {
		var r = jd();
		n = {
			lane: r,
			revertLane: 0,
			gesture: null,
			action: n,
			hasEagerState: !1,
			eagerState: null,
			next: null
		}, pc(e) ? mc(t, n) : (n = Oi(e, t, n, r), n !== null && (Pd(n, e, r), hc(n, t, r)));
	}
	function uc(e, t, n) {
		dc(e, t, n, jd());
	}
	function dc(e, t, n, r) {
		var i = {
			lane: r,
			revertLane: 0,
			gesture: null,
			action: n,
			hasEagerState: !1,
			eagerState: null,
			next: null
		};
		if (pc(e)) mc(t, i);
		else {
			var a = e.alternate;
			if (e.lanes === 0 && (a === null || a.lanes === 0) && (a = t.lastRenderedReducer, a !== null)) try {
				var o = t.lastRenderedState, s = a(o, n);
				if (i.hasEagerState = !0, i.eagerState = s, Ur(s, o)) return Di(e, t, i, 0), K === null && Ei(), !1;
			} catch {}
			if (n = Oi(e, t, i, r), n !== null) return Pd(n, e, r), hc(n, t, r), !0;
		}
		return !1;
	}
	function fc(e, t, n, r) {
		if (r = {
			lane: 2,
			revertLane: Pf(),
			gesture: null,
			action: r,
			hasEagerState: !1,
			eagerState: null,
			next: null
		}, pc(e)) {
			if (t) throw Error(i(479));
		} else t = Oi(e, n, r, 2), t !== null && Pd(t, e, 2);
	}
	function pc(e) {
		var t = e.alternate;
		return e === B || t !== null && t === B;
	}
	function mc(e, t) {
		Ko = Go = !0;
		var n = e.pending;
		n === null ? t.next = t : (t.next = n.next, n.next = t), e.pending = t;
	}
	function hc(e, t, n) {
		if (n & 4194048) {
			var r = t.lanes;
			r &= e.pendingLanes, n |= r, t.lanes = n, vt(e, n);
		}
	}
	var gc = {
		readContext: Ea,
		use: ds,
		useCallback: Qo,
		useContext: Qo,
		useEffect: Qo,
		useImperativeHandle: Qo,
		useLayoutEffect: Qo,
		useInsertionEffect: Qo,
		useMemo: Qo,
		useReducer: Qo,
		useRef: Qo,
		useState: Qo,
		useDebugValue: Qo,
		useDeferredValue: Qo,
		useTransition: Qo,
		useSyncExternalStore: Qo,
		useId: Qo,
		useHostTransitionStatus: Qo,
		useFormState: Qo,
		useActionState: Qo,
		useOptimistic: Qo,
		useMemoCache: Qo,
		useCacheRefresh: Qo,
		useEffectEvent: Qo
	}, _c = {
		readContext: Ea,
		use: ds,
		useCallback: function(e, t) {
			return ss().memoizedState = [e, t === void 0 ? null : t], e;
		},
		useContext: Ea,
		useEffect: Vs,
		useImperativeHandle: function(e, t, n) {
			n = n == null ? null : n.concat([e]), zs(4194308, 4, qs.bind(null, t, e), n);
		},
		useLayoutEffect: function(e, t) {
			return zs(4194308, 4, e, t);
		},
		useInsertionEffect: function(e, t) {
			zs(4, 2, e, t);
		},
		useMemo: function(e, t) {
			var n = ss();
			t = t === void 0 ? null : t;
			var r = e();
			if (qo) {
				et(!0);
				try {
					e();
				} finally {
					et(!1);
				}
			}
			return n.memoizedState = [r, t], r;
		},
		useReducer: function(e, t, n) {
			var r = ss();
			if (n !== void 0) {
				var i = n(t);
				if (qo) {
					et(!0);
					try {
						n(t);
					} finally {
						et(!1);
					}
				}
			} else i = t;
			return r.memoizedState = r.baseState = i, e = {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: e,
				lastRenderedState: i
			}, r.queue = e, e = e.dispatch = lc.bind(null, B, e), [r.memoizedState, e];
		},
		useRef: function(e) {
			var t = ss();
			return e = { current: e }, t.memoizedState = e;
		},
		useState: function(e) {
			e = Cs(e);
			var t = e.queue, n = uc.bind(null, B, t);
			return t.dispatch = n, [e.memoizedState, n];
		},
		useDebugValue: Ys,
		useDeferredValue: function(e, t) {
			return Qs(ss(), e, t);
		},
		useTransition: function() {
			var e = Cs(!1);
			return e = ec.bind(null, B, e.queue, !0, !1), ss().memoizedState = e, [!1, e];
		},
		useSyncExternalStore: function(e, t, n) {
			var r = B, a = ss();
			if (z) {
				if (n === void 0) throw Error(i(407));
				n = n();
			} else {
				if (n = t(), K === null) throw Error(i(349));
				J & 127 || vs(r, t, n);
			}
			a.memoizedState = n;
			var o = {
				value: n,
				getSnapshot: t
			};
			return a.queue = o, Vs(bs.bind(null, r, o, e), [e]), r.flags |= 2048, Ls(9, { destroy: void 0 }, ys.bind(null, r, o, n, t), null), n;
		},
		useId: function() {
			var e = ss(), t = K.identifierPrefix;
			if (z) {
				var n = $i, r = Qi;
				n = (r & ~(1 << 32 - tt(r) - 1)).toString(32) + n, t = "_" + t + "R_" + n, n = Jo++, 0 < n && (t += "H" + n.toString(32)), t += "_";
			} else n = Zo++, t = "_" + t + "r_" + n.toString(32) + "_";
			return e.memoizedState = t;
		},
		useHostTransitionStatus: ac,
		useFormState: Ms,
		useActionState: Ms,
		useOptimistic: function(e) {
			var t = ss();
			t.memoizedState = t.baseState = e;
			var n = {
				pending: null,
				lanes: 0,
				dispatch: null,
				lastRenderedReducer: null,
				lastRenderedState: null
			};
			return t.queue = n, t = fc.bind(null, B, !0, n), n.dispatch = t, [e, t];
		},
		useMemoCache: fs,
		useCacheRefresh: function() {
			return ss().memoizedState = cc.bind(null, B);
		},
		useEffectEvent: function(e) {
			var t = ss(), n = { impl: e };
			return t.memoizedState = n, function() {
				if (G & 2) throw Error(i(440));
				return n.impl.apply(void 0, arguments);
			};
		}
	}, vc = {
		readContext: Ea,
		use: ds,
		useCallback: Xs,
		useContext: Ea,
		useEffect: Hs,
		useImperativeHandle: Js,
		useInsertionEffect: Gs,
		useLayoutEffect: Ks,
		useMemo: Zs,
		useReducer: ms,
		useRef: Rs,
		useState: function() {
			return ms(ps);
		},
		useDebugValue: Ys,
		useDeferredValue: function(e, t) {
			return $s(cs(), V.memoizedState, e, t);
		},
		useTransition: function() {
			var e = ms(ps)[0], t = cs().memoizedState;
			return [typeof e == "boolean" ? e : us(e), t];
		},
		useSyncExternalStore: _s,
		useId: oc,
		useHostTransitionStatus: ac,
		useFormState: Ns,
		useActionState: Ns,
		useOptimistic: function(e, t) {
			return ws(cs(), V, e, t);
		},
		useMemoCache: fs,
		useCacheRefresh: sc,
		useEffectEvent: Ws
	}, yc = {
		readContext: Ea,
		use: ds,
		useCallback: Xs,
		useContext: Ea,
		useEffect: Hs,
		useImperativeHandle: Js,
		useInsertionEffect: Gs,
		useLayoutEffect: Ks,
		useMemo: Zs,
		useReducer: gs,
		useRef: Rs,
		useState: function() {
			return gs(ps);
		},
		useDebugValue: Ys,
		useDeferredValue: function(e, t) {
			var n = cs();
			return V === null ? Qs(n, e, t) : $s(n, V.memoizedState, e, t);
		},
		useTransition: function() {
			var e = gs(ps)[0], t = cs().memoizedState;
			return [typeof e == "boolean" ? e : us(e), t];
		},
		useSyncExternalStore: _s,
		useId: oc,
		useHostTransitionStatus: ac,
		useFormState: Is,
		useActionState: Is,
		useOptimistic: function(e, t) {
			var n = cs();
			return V === null ? (n.baseState = e, [e, n.queue.dispatch]) : ws(n, V, e, t);
		},
		useMemoCache: fs,
		useCacheRefresh: sc,
		useEffectEvent: Ws
	};
	function bc(e, t, n, r) {
		t = e.memoizedState, n = n(r, t), n = n == null ? t : D({}, t, n), e.memoizedState = n, e.lanes === 0 && (e.updateQueue.baseState = n);
	}
	var xc = {
		enqueueSetState: function(e, t, n) {
			e = e._reactInternals;
			var r = jd(), i = vo(r);
			i.payload = t, n != null && (i.callback = n), t = yo(e, i, r), t !== null && (Pd(t, e, r), bo(t, e, r));
		},
		enqueueReplaceState: function(e, t, n) {
			e = e._reactInternals;
			var r = jd(), i = vo(r);
			i.tag = 1, i.payload = t, n != null && (i.callback = n), t = yo(e, i, r), t !== null && (Pd(t, e, r), bo(t, e, r));
		},
		enqueueForceUpdate: function(e, t) {
			e = e._reactInternals;
			var n = jd(), r = vo(n);
			r.tag = 2, t != null && (r.callback = t), t = yo(e, r, n), t !== null && (Pd(t, e, n), bo(t, e, n));
		}
	};
	function Sc(e, t, n, r, i, a, o) {
		return e = e.stateNode, typeof e.shouldComponentUpdate == "function" ? e.shouldComponentUpdate(r, a, o) : t.prototype && t.prototype.isPureReactComponent ? !Wr(n, r) || !Wr(i, a) : !0;
	}
	function Cc(e, t, n, r) {
		e = t.state, typeof t.componentWillReceiveProps == "function" && t.componentWillReceiveProps(n, r), typeof t.UNSAFE_componentWillReceiveProps == "function" && t.UNSAFE_componentWillReceiveProps(n, r), t.state !== e && xc.enqueueReplaceState(t, t.state, null);
	}
	function wc(e, t) {
		var n = t;
		if ("ref" in t) for (var r in n = {}, t) r !== "ref" && (n[r] = t[r]);
		if (e = e.defaultProps) for (var i in n === t && (n = D({}, n)), e) n[i] === void 0 && (n[i] = e[i]);
		return n;
	}
	function Tc(e) {
		Si(e);
	}
	function Ec(e) {
		console.error(e);
	}
	function Dc(e) {
		Si(e);
	}
	function Oc(e, t) {
		try {
			var n = e.onUncaughtError;
			n(t.value, { componentStack: t.stack });
		} catch (e) {
			setTimeout(function() {
				throw e;
			});
		}
	}
	function kc(e, t, n) {
		try {
			var r = e.onCaughtError;
			r(n.value, {
				componentStack: n.stack,
				errorBoundary: t.tag === 1 ? t.stateNode : null
			});
		} catch (e) {
			setTimeout(function() {
				throw e;
			});
		}
	}
	function Ac(e, t, n) {
		return n = vo(n), n.tag = 3, n.payload = { element: null }, n.callback = function() {
			Oc(e, t);
		}, n;
	}
	function jc(e) {
		return e = vo(e), e.tag = 3, e;
	}
	function Mc(e, t, n, r) {
		var i = n.type.getDerivedStateFromError;
		if (typeof i == "function") {
			var a = r.value;
			e.payload = function() {
				return i(a);
			}, e.callback = function() {
				kc(t, n, r);
			};
		}
		var o = n.stateNode;
		o !== null && typeof o.componentDidCatch == "function" && (e.callback = function() {
			kc(t, n, r), typeof i != "function" && (yd === null ? yd = /* @__PURE__ */ new Set([this]) : yd.add(this));
			var e = r.stack;
			this.componentDidCatch(r.value, { componentStack: e === null ? "" : e });
		});
	}
	function Nc(e, t, n, r, a) {
		if (n.flags |= 32768, typeof r == "object" && r && typeof r.then == "function") {
			if (t = n.alternate, t !== null && Ca(t, n, a, !0), n = Mo.current, n !== null) {
				switch (n.tag) {
					case 31:
					case 13:
					case 19: return No === null ? Kd() : n.alternate === null && od === 0 && (od = 3), n.flags &= -257, n.flags |= 65536, n.lanes = a, r === $a ? n.flags |= 16384 : (t = n.updateQueue, t === null ? n.updateQueue = /* @__PURE__ */ new Set([r]) : t.add(r), mf(e, r, a)), !1;
					case 22: return n.flags |= 65536, r === $a ? n.flags |= 16384 : (t = n.updateQueue, t === null ? (t = {
						transitions: null,
						markerInstances: null,
						retryQueue: /* @__PURE__ */ new Set([r])
					}, n.updateQueue = t) : (n = t.retryQueue, n === null ? t.retryQueue = /* @__PURE__ */ new Set([r]) : n.add(r)), mf(e, r, a)), !1;
				}
				throw Error(i(435, n.tag));
			}
			return mf(e, r, a), Kd(), !1;
		}
		if (z) return t = Mo.current, t === null ? (r !== ca && (t = Error(i(423), { cause: r }), ha(Wi(t, n))), e = e.current.alternate, e.flags |= 65536, a &= -a, e.lanes |= a, r = Wi(r, n), a = Ac(e.stateNode, r, a), xo(e, a), od !== 4 && (od = 2)) : (!(t.flags & 65536) && (t.flags |= 256), t.flags |= 65536, t.lanes = a, r !== ca && (e = Error(i(422), { cause: r }), ha(Wi(e, n)))), !1;
		var o = Error(i(520), { cause: r });
		if (o = Wi(o, n), fd === null ? fd = [o] : fd.push(o), od !== 4 && (od = 2), t === null) return !0;
		r = Wi(r, n), n = t;
		do {
			switch (n.tag) {
				case 3: return n.flags |= 65536, e = a & -a, n.lanes |= e, e = Ac(n.stateNode, r, e), xo(n, e), !1;
				case 1:
					if (t = n.type, o = n.stateNode, !(n.flags & 128) && (typeof t.getDerivedStateFromError == "function" || o !== null && typeof o.componentDidCatch == "function" && (yd === null || !yd.has(o)))) return n.flags |= 65536, a &= -a, n.lanes |= a, a = jc(a), Mc(a, e, n, r), xo(n, a), !1;
					break;
				case 22: if (n.memoizedState !== null) return n.flags |= 65536, !1;
			}
			n = n.return;
		} while (n !== null);
		return !1;
	}
	var Pc = Error(i(461)), Fc = !1;
	function Ic(e, t, n, r) {
		t.child = e === null ? mo(t, null, n, r) : po(t, e.child, n, r);
	}
	function Lc(e, t, n, r, i) {
		n = n.render;
		var a = t.ref;
		if ("ref" in r) {
			var o = {};
			for (var s in r) s !== "ref" && (o[s] = r[s]);
		} else o = r;
		return Ta(t), r = es(e, t, n, o, a, i), s = is(), e !== null && !Fc ? (as(e, t, i), dl(e, t, i)) : (z && s && na(t), t.flags |= 1, Ic(e, t, r, i), t.child);
	}
	function Rc(e, t, n, r, i) {
		if (e === null) {
			var a = n.type;
			return typeof a == "function" && !Fi(a) && a.defaultProps === void 0 && n.compare === null ? (t.tag = 15, t.type = a, zc(e, t, a, r, i)) : (e = Ri(n.type, null, r, t, t.mode, i), e.ref = t.ref, e.return = t, t.child = e);
		}
		if (a = e.child, !fl(e, i)) {
			var o = a.memoizedProps;
			if (n = n.compare, n = n === null ? Wr : n, n(o, r) && e.ref === t.ref) return dl(e, t, i);
		}
		return t.flags |= 1, e = Ii(a, r), e.ref = t.ref, e.return = t, t.child = e;
	}
	function zc(e, t, n, r, i) {
		if (e !== null) {
			var a = e.memoizedProps;
			if (Wr(a, r) && e.ref === t.ref) {
				if (Fc = !1, t.pendingProps = r = a, fl(e, i)) e.flags & 131072 && (Fc = !0);
				else return t.lanes = e.lanes, dl(e, t, i);
			}
		}
		return qc(e, t, n, r, i);
	}
	function Bc(e, t, n, r) {
		var i = r.children, a = e === null ? null : e.memoizedState;
		if (e === null && t.stateNode === null && (t.stateNode = {
			_visibility: 1,
			_pendingMarkers: null,
			_retryCache: null,
			_transitions: null
		}), r.mode === "hidden") {
			if (t.flags & 128) {
				if (a = a === null ? n : a.baseLanes | n, e !== null) {
					for (r = t.child = e.child, i = 0; r !== null;) i = i | r.lanes | r.childLanes, r = r.sibling;
					r = i & ~a;
				} else r = 0, t.child = null;
				return Hc(e, t, a, n, r);
			}
			if (n & 536870912) t.memoizedState = {
				baseLanes: 0,
				cachePool: null
			}, e !== null && Ja(t, a === null ? null : a.cachePool), a === null ? Ao() : ko(t, a), Io(t);
			else return r = t.lanes = 536870912, Hc(e, t, a === null ? n : a.baseLanes | n, n, r);
		} else a === null ? (e !== null && Ja(t, null), Ao(), Lo()) : (Ja(t, a.cachePool), ko(t, a), Lo(), t.memoizedState = null);
		return Ic(e, t, i, n), t.child;
	}
	function Vc(e, t) {
		return e !== null && e.tag === 22 || t.stateNode !== null || (t.stateNode = {
			_visibility: 1,
			_pendingMarkers: null,
			_retryCache: null,
			_transitions: null
		}), t.sibling;
	}
	function Hc(e, t, n, r, i) {
		var a = qa();
		return a = a === null ? null : {
			parent: Ma._currentValue,
			pool: a
		}, t.memoizedState = {
			baseLanes: n,
			cachePool: a
		}, e !== null && Ja(t, null), Ao(), Io(t), e !== null && Ca(e, t, r, !0), t.childLanes = i, null;
	}
	function Uc(e, t) {
		return t = nl({
			mode: t.mode,
			children: t.children
		}, e.mode), t.ref = e.ref, e.child = t, t.return = e, t;
	}
	function Wc(e, t, n) {
		return po(t, e.child, null, n), e = Uc(t, t.pendingProps), e.flags |= 2, Ro(t), t.memoizedState = null, e;
	}
	function Gc(e, t, n) {
		var r = t.pendingProps, a = !!(t.flags & 128);
		if (t.flags &= -129, e === null) {
			if (z) {
				if (r.mode === "hidden") return e = Uc(t, r), t.lanes = 536870912, e.memoizedState = {
					baseLanes: 0,
					cachePool: null
				}, Vc(null, e);
				if (Fo(t), (e = R) ? (e = am(e, sa), e = e !== null && e.data === "&" ? e : null, e !== null && (t.memoizedState = {
					dehydrated: e,
					treeContext: Zi === null ? null : {
						id: Qi,
						overflow: $i
					},
					retryLane: 536870912,
					hydrationErrors: null
				}, n = Vi(e), n.return = t, t.child = n, aa = t, R = null)) : e = null, e === null) throw la(t);
				return t.lanes = 536870912, null;
			}
			return Uc(t, r);
		}
		var o = e.memoizedState;
		if (o !== null) {
			var s = o.dehydrated;
			if (Fo(t), a) {
				if (t.flags & 256) t.flags &= -257, t = Wc(e, t, n);
				else if (t.memoizedState !== null) t.child = e.child, t.flags |= 128, t = null;
				else throw Error(i(558));
			} else if (Fc || Ca(e, t, n, !1), a = (n & e.childLanes) !== 0, Fc || a) {
				if (Do.current === null) {
					if (r = K, r !== null && (s = yt(r, n), s !== 0 && s !== o.retryLane)) throw o.retryLane = s, ki(e, s), Pd(r, e, s), Pc;
					Kd();
				}
				t = Wc(e, t, n);
			} else e = o.treeContext, R = lm(s.nextSibling), aa = t, z = !0, oa = null, sa = !1, e !== null && ia(t, e), t = Uc(t, r), t.flags |= 134221824;
			return t;
		}
		return e = Ii(e.child, {
			mode: r.mode,
			children: r.children
		}), e.ref = t.ref, t.child = e, e.return = t, e;
	}
	function Kc(e, t) {
		var n = t.ref;
		if (n === null) e !== null && e.ref !== null && (t.flags |= 4194816);
		else {
			if (typeof n != "function" && typeof n != "object") throw Error(i(284));
			(e === null || e.ref !== n) && (t.flags |= 4194816);
		}
	}
	function qc(e, t, n, r, i) {
		return Ta(t), n = es(e, t, n, r, void 0, i), r = is(), e !== null && !Fc ? (as(e, t, i), dl(e, t, i)) : (z && r && na(t), t.flags |= 1, Ic(e, t, n, i), t.child);
	}
	function Jc(e, t, n, r, i, a) {
		return Ta(t), t.updateQueue = null, n = ns(t, r, n, i), ts(e), r = is(), e !== null && !Fc ? (as(e, t, a), dl(e, t, a)) : (z && r && na(t), t.flags |= 1, Ic(e, t, n, a), t.child);
	}
	function Yc(e, t, n, r, i) {
		if (Ta(t), t.stateNode === null) {
			var a = Mi, o = n.contextType;
			typeof o == "object" && o && (a = Ea(o)), a = new n(r, a), t.memoizedState = a.state !== null && a.state !== void 0 ? a.state : null, a.updater = xc, t.stateNode = a, a._reactInternals = t, a = t.stateNode, a.props = r, a.state = t.memoizedState, a.refs = {}, go(t), o = n.contextType, a.context = typeof o == "object" && o ? Ea(o) : Mi, a.state = t.memoizedState, o = n.getDerivedStateFromProps, typeof o == "function" && (bc(t, n, o, r), a.state = t.memoizedState), typeof n.getDerivedStateFromProps == "function" || typeof a.getSnapshotBeforeUpdate == "function" || typeof a.UNSAFE_componentWillMount != "function" && typeof a.componentWillMount != "function" || (o = a.state, typeof a.componentWillMount == "function" && a.componentWillMount(), typeof a.UNSAFE_componentWillMount == "function" && a.UNSAFE_componentWillMount(), o !== a.state && xc.enqueueReplaceState(a, a.state, null), wo(t, r, a, i), Co(), a.state = t.memoizedState), typeof a.componentDidMount == "function" && (t.flags |= 4194308), r = !0;
		} else if (e === null) {
			a = t.stateNode;
			var s = t.memoizedProps, c = wc(n, s);
			a.props = c;
			var l = a.context, u = n.contextType;
			o = Mi, typeof u == "object" && u && (o = Ea(u));
			var d = n.getDerivedStateFromProps;
			u = typeof d == "function" || typeof a.getSnapshotBeforeUpdate == "function", s = t.pendingProps !== s, u || typeof a.UNSAFE_componentWillReceiveProps != "function" && typeof a.componentWillReceiveProps != "function" || (s || l !== o) && Cc(t, a, r, o), ho = !1;
			var f = t.memoizedState;
			a.state = f, wo(t, r, a, i), Co(), l = t.memoizedState, s || f !== l || ho ? (typeof d == "function" && (bc(t, n, d, r), l = t.memoizedState), (c = ho || Sc(t, n, c, r, f, l, o)) ? (u || typeof a.UNSAFE_componentWillMount != "function" && typeof a.componentWillMount != "function" || (typeof a.componentWillMount == "function" && a.componentWillMount(), typeof a.UNSAFE_componentWillMount == "function" && a.UNSAFE_componentWillMount()), typeof a.componentDidMount == "function" && (t.flags |= 4194308)) : (typeof a.componentDidMount == "function" && (t.flags |= 4194308), t.memoizedProps = r, t.memoizedState = l), a.props = r, a.state = l, a.context = o, r = c) : (typeof a.componentDidMount == "function" && (t.flags |= 4194308), r = !1);
		} else {
			a = t.stateNode, _o(e, t), o = t.memoizedProps, u = wc(n, o), a.props = u, d = t.pendingProps, f = a.context, l = n.contextType, c = Mi, typeof l == "object" && l && (c = Ea(l)), s = n.getDerivedStateFromProps, (l = typeof s == "function" || typeof a.getSnapshotBeforeUpdate == "function") || typeof a.UNSAFE_componentWillReceiveProps != "function" && typeof a.componentWillReceiveProps != "function" || (o !== d || f !== c) && Cc(t, a, r, c), ho = !1, f = t.memoizedState, a.state = f, wo(t, r, a, i), Co();
			var p = t.memoizedState;
			o !== d || f !== p || ho || e !== null && e.dependencies !== null && wa(e.dependencies) ? (typeof s == "function" && (bc(t, n, s, r), p = t.memoizedState), (u = ho || Sc(t, n, u, r, f, p, c) || e !== null && e.dependencies !== null && wa(e.dependencies)) ? (l || typeof a.UNSAFE_componentWillUpdate != "function" && typeof a.componentWillUpdate != "function" || (typeof a.componentWillUpdate == "function" && a.componentWillUpdate(r, p, c), typeof a.UNSAFE_componentWillUpdate == "function" && a.UNSAFE_componentWillUpdate(r, p, c)), typeof a.componentDidUpdate == "function" && (t.flags |= 4), typeof a.getSnapshotBeforeUpdate == "function" && (t.flags |= 1024)) : (typeof a.componentDidUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 4), typeof a.getSnapshotBeforeUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 1024), t.memoizedProps = r, t.memoizedState = p), a.props = r, a.state = p, a.context = c, r = u) : (typeof a.componentDidUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 4), typeof a.getSnapshotBeforeUpdate != "function" || o === e.memoizedProps && f === e.memoizedState || (t.flags |= 1024), r = !1);
		}
		return a = r, Kc(e, t), r = !!(t.flags & 128), a || r ? (a = t.stateNode, n = r && typeof n.getDerivedStateFromError != "function" ? null : a.render(), t.flags |= 1, e !== null && r ? (t.child = po(t, e.child, null, i), t.child = po(t, null, n, i)) : Ic(e, t, n, i), t.memoizedState = a.state, e = t.child) : e = dl(e, t, i), e;
	}
	function Xc(e, t, n, r) {
		return pa(), t.flags |= 256, Ic(e, t, n, r), t.child;
	}
	var Zc = {
		dehydrated: null,
		treeContext: null,
		retryLane: 0,
		hydrationErrors: null
	};
	function Qc(e) {
		return {
			baseLanes: e,
			cachePool: Ya()
		};
	}
	function $c(e, t, n) {
		return e = e === null ? 0 : e.childLanes & ~n, t && (e |= ud), e;
	}
	function el(e, t, n) {
		var r = t.pendingProps, i = !1, a = !!(t.flags & 128), o;
		if ((o = a) || (o = e !== null && e.memoizedState === null ? !1 : !!(zo.current & 2)), o && (i = !0, t.flags &= -129), o = !!(t.flags & 32), t.flags &= -33, e === null) {
			if (z) {
				if (i ? Po(t) : Lo(), (e = R) ? (e = am(e, sa), e = e !== null && e.data !== "&" ? e : null, e !== null && (t.memoizedState = {
					dehydrated: e,
					treeContext: Zi === null ? null : {
						id: Qi,
						overflow: $i
					},
					retryLane: 536870912,
					hydrationErrors: null
				}, n = Vi(e), n.return = t, t.child = n, aa = t, R = null)) : e = null, e === null) throw la(t);
				return t.lanes = sm(e) ? 32 : 536870912, null;
			}
			return a = r.children, r = r.fallback, i ? (Lo(), i = t.mode, a = nl({
				mode: "hidden",
				children: a
			}, i), r = zi(r, i, n, null), a.return = t, r.return = t, a.sibling = r, t.child = a, r = t.child, r.memoizedState = Qc(n), r.childLanes = $c(e, o, n), t.memoizedState = Zc, Vc(null, r)) : (Po(t), tl(t, a));
		}
		var s = e.memoizedState;
		if (s !== null) {
			var c = s.dehydrated;
			if (c !== null) return il(e, t, a, o, r, c, s, n);
		}
		return i ? (Lo(), i = r.fallback, a = t.mode, s = e.child, c = s.sibling, r = Ii(s, {
			mode: "hidden",
			children: r.children
		}), r.subtreeFlags = s.subtreeFlags & 1206910976, c === null ? (i = zi(i, a, n, null), i.flags |= 2) : i = Ii(c, i), i.return = t, r.return = t, r.sibling = i, t.child = r, Vc(null, r), r = t.child, i = e.child.memoizedState, i === null ? i = Qc(n) : (a = i.cachePool, a === null ? a = Ya() : (s = Ma._currentValue, a = a.parent === s ? a : {
			parent: s,
			pool: s
		}), i = {
			baseLanes: i.baseLanes | n,
			cachePool: a
		}), r.memoizedState = i, r.childLanes = $c(e, o, n), t.memoizedState = Zc, Vc(e.child, r)) : (Po(t), n = e.child, e = n.sibling, n = Ii(n, {
			mode: "visible",
			children: r.children
		}), n.return = t, n.sibling = null, e !== null && (o = t.deletions, o === null ? (t.deletions = [e], t.flags |= 16) : o.push(e)), t.child = n, t.memoizedState = null, n);
	}
	function tl(e, t) {
		return t = nl({
			mode: "visible",
			children: t
		}, e.mode), t.return = e, e.child = t;
	}
	function nl(e, t) {
		return e = Pi(22, e, null, t), e.lanes = 0, e;
	}
	function rl(e, t, n) {
		return po(t, e.child, null, n), e = tl(t, t.pendingProps.children), e.flags |= 2, t.memoizedState = null, e;
	}
	function il(e, t, n, r, a, o, s, c) {
		if (n) return t.flags & 256 ? (Po(t), t.flags &= -257, rl(e, t, c)) : t.memoizedState === null ? (Lo(), o = a.fallback, s = t.mode, a = nl({
			mode: "visible",
			children: a.children
		}, s), o = zi(o, s, c, null), o.flags |= 2, a.return = t, o.return = t, a.sibling = o, t.child = a, po(t, e.child, null, c), a = t.child, a.memoizedState = Qc(c), a.childLanes = $c(e, r, c), t.memoizedState = Zc, Vc(null, a)) : (Lo(), t.child = e.child, t.flags |= 128, null);
		if (Po(t), sm(o)) {
			if (r = o.nextSibling && o.nextSibling.dataset, r) var l = r.dgst;
			return r = l, r !== "" && (a = Error(i(419)), a.stack = "", a.digest = r, ha({
				value: a,
				source: null,
				stack: null
			})), rl(e, t, c);
		}
		if (Fc || Ca(e, t, c, !1), r = (c & e.childLanes) !== 0, Fc || r) {
			if (Do.current !== null) return rl(e, t, c);
			if (r = K, r !== null && (a = yt(r, c), a !== 0 && a !== s.retryLane)) throw s.retryLane = a, ki(e, a), Pd(r, e, a), Pc;
			return om(o) || Kd(), rl(e, t, c);
		}
		return om(o) ? (t.flags |= 192, t.child = e.child, null) : (e = s.treeContext, R = lm(o.nextSibling), aa = t, z = !0, oa = null, sa = !1, e !== null && ia(t, e), t = tl(t, a.children), t.flags |= 134221824, t);
	}
	function al(e, t, n) {
		e.lanes |= t;
		var r = e.alternate;
		r !== null && (r.lanes |= t), xa(e.return, t, n);
	}
	function ol(e) {
		for (var t = null; e !== null;) {
			var n = e.alternate;
			n !== null && Ho(n) === null && (t = e), e = e.sibling;
		}
		return t;
	}
	function sl(e, t, n, r, i, a) {
		var o = e.memoizedState;
		o === null ? e.memoizedState = {
			isBackwards: t,
			rendering: null,
			renderingStartTime: 0,
			last: r,
			tail: n,
			tailMode: i,
			treeForkCount: a
		} : (o.isBackwards = t, o.rendering = null, o.renderingStartTime = 0, o.last = r, o.tail = n, o.tailMode = i, o.treeForkCount = a);
	}
	function cl(e) {
		var t = e.child;
		for (e.child = null; t !== null;) {
			var n = t.sibling;
			t.sibling = e.child, e.child = t, t = n;
		}
	}
	function ll(e, t, n) {
		var r = t.pendingProps, i = r.revealOrder, a = r.tail;
		r = r.children;
		var o = zo.current;
		if (t.flags & 128) return Bo(t, o), null;
		var s = !!(o & 2);
		if (s ? (o = o & 1 | 2, t.flags |= 128) : o &= 1, Bo(t, o), i === "backwards" && e !== null ? (cl(e), Ic(e, t, r, n), cl(e)) : Ic(e, t, r, n), r = z ? Ji : 0, !s && e !== null && e.flags & 128) a: for (e = t.child; e !== null;) {
			if (e.tag === 13) e.memoizedState !== null && al(e, n, t);
			else if (e.tag === 19) al(e, n, t);
			else if (e.child !== null) {
				e.child.return = e, e = e.child;
				continue;
			}
			if (e === t) break a;
			for (; e.sibling === null;) {
				if (e.return === null || e.return === t) break a;
				e = e.return;
			}
			e.sibling.return = e.return, e = e.sibling;
		}
		switch (i) {
			case "backwards":
				n = ol(t.child), n === null ? (i = t.child, t.child = null) : (i = n.sibling, n.sibling = null, cl(t)), sl(t, !0, i, null, a, r);
				break;
			case "unstable_legacy-backwards":
				for (n = null, i = t.child, t.child = null; i !== null;) {
					if (e = i.alternate, e !== null && Ho(e) === null) {
						t.child = i;
						break;
					}
					e = i.sibling, i.sibling = n, n = i, i = e;
				}
				sl(t, !0, n, null, a, r);
				break;
			case "together":
				sl(t, !1, null, null, void 0, r);
				break;
			case "independent":
				t.memoizedState = null;
				break;
			default: n = ol(t.child), n === null ? (i = t.child, t.child = null) : (i = n.sibling, n.sibling = null), sl(t, !1, i, n, a, r);
		}
		return t.child;
	}
	function ul(e, t, n) {
		var r = t.pendingProps;
		return ya(t, t.type, r.value), Ic(e, t, r.children, n), t.child;
	}
	function dl(e, t, n) {
		if (e !== null && (t.dependencies = e.dependencies), sd |= t.lanes, (n & t.childLanes) === 0) {
			if (e !== null) {
				if (Ca(e, t, n, !1), (n & t.childLanes) === 0) return null;
			} else return null;
		}
		if (e !== null && t.child !== e.child) throw Error(i(153));
		if (t.child !== null) {
			for (e = t.child, n = Ii(e, e.pendingProps), t.child = n, n.return = t; e.sibling !== null;) e = e.sibling, n = n.sibling = Ii(e, e.pendingProps), n.return = t;
			n.sibling = null;
		}
		return t.child;
	}
	function fl(e, t) {
		return (e.lanes & t) !== 0 || (e = e.dependencies, !!(e !== null && wa(e)));
	}
	function pl(e, t, n) {
		switch (t.tag) {
			case 3:
				De(t, t.stateNode.containerInfo), ya(t, Ma, e.memoizedState.cache), pa();
				break;
			case 27:
			case 5:
				ke(t);
				break;
			case 4:
				De(t, t.stateNode.containerInfo);
				break;
			case 10:
				ya(t, t.type, t.memoizedProps.value);
				break;
			case 31:
				if (t.memoizedState !== null) return t.flags |= 128, Fo(t), null;
				break;
			case 13:
				var r = t.memoizedState;
				if (r !== null) {
					if (r.dehydrated !== null) return Po(t), t.flags |= 128, null;
					r = Ca(e, t, n, !1);
					var i = t.child.childLanes;
					return r || (n & i) !== 0 ? el(e, t, n) : (Po(t), e = dl(e, t, n), e === null ? null : e.sibling);
				}
				Po(t);
				break;
			case 19:
				if (t.flags & 128) return ll(e, t, n);
				if (i = !!(e.flags & 128), r = (n & t.childLanes) !== 0, r ||= (Ca(e, t, n, !1), (n & t.childLanes) !== 0), i) {
					if (r) return ll(e, t, n);
					t.flags |= 128;
				}
				if (i = t.memoizedState, i !== null && (i.rendering = null, i.tail = null, i.lastEffect = null), Bo(t, zo.current), r) break;
				return null;
			case 22: return t.lanes = 0, Bc(e, t, n, t.pendingProps);
			case 24: ya(t, Ma, e.memoizedState.cache);
		}
		return dl(e, t, n);
	}
	function ml(e, t, n) {
		if (e !== null) {
			if (e.memoizedProps !== t.pendingProps) Fc = !0;
			else {
				if (!fl(e, n) && !(t.flags & 128)) return Fc = !1, pl(e, t, n);
				Fc = !!(e.flags & 131072);
			}
		} else Fc = !1, z && t.flags & 1048576 && ta(t, Ji, t.index);
		switch (t.lanes = 0, t.tag) {
			case 16:
				a: {
					var r = t.pendingProps;
					if (e = no(t.elementType), t.type = e, typeof e == "function") Fi(e) ? (r = wc(e, r), t.tag = 1, t = Yc(null, t, e, r, n)) : (t.tag = 0, t = qc(null, t, e, r, n));
					else {
						if (e != null) {
							var a = e.$$typeof;
							if (a === M) {
								t.tag = 11, t = Lc(null, t, e, r, n);
								break a;
							}
							if (a === oe) {
								t.tag = 14, t = Rc(null, t, e, r, n);
								break a;
							}
							if (a === j) {
								t.tag = 10, t.type = e, t = ul(null, t, n);
								break a;
							}
						}
						throw t = ge(e) || e, Error(i(306, t, ""));
					}
				}
				return t;
			case 0: return qc(e, t, t.type, t.pendingProps, n);
			case 1: return r = t.type, a = wc(r, t.pendingProps), Yc(e, t, r, a, n);
			case 3:
				a: {
					if (De(t, t.stateNode.containerInfo), e === null) throw Error(i(387));
					r = t.pendingProps;
					var o = t.memoizedState;
					a = o.element, _o(e, t), wo(t, r, null, n);
					var s = t.memoizedState;
					if (r = s.cache, ya(t, Ma, r), r !== o.cache && Sa(t, [Ma], n, !0), Co(), r = s.element, o.isDehydrated) {
						if (o = {
							element: r,
							isDehydrated: !1,
							cache: s.cache
						}, t.updateQueue.baseState = o, t.memoizedState = o, t.flags & 256) {
							t = Xc(e, t, r, n);
							break a;
						}
						if (r !== a) {
							a = Wi(Error(i(424)), t), ha(a), t = Xc(e, t, r, n);
							break a;
						}
						switch (e = t.stateNode.containerInfo, e.nodeType) {
							case 9:
								e = e.body;
								break;
							default: e = e.nodeName === "HTML" ? e.ownerDocument.body : e;
						}
						for (R = lm(e.firstChild), aa = t, z = !0, oa = null, sa = !0, n = mo(t, null, r, n), t.child = n; n;) n.flags = n.flags & -3 | 134221824, n = n.sibling;
					} else {
						if (pa(), r === a) {
							t = dl(e, t, n);
							break a;
						}
						Ic(e, t, r, n);
					}
					t = t.child;
				}
				return t;
			case 26: return Kc(e, t), e === null ? (n = Nm(t.type, null, t.pendingProps, null)) ? t.memoizedState = n : z || (t.stateNode = fp(t.type, t.pendingProps, Te.current, t)) : t.memoizedState = Nm(t.type, e.memoizedProps, t.pendingProps, e.memoizedState), null;
			case 27: return ke(t), e === null && z && (r = t.stateNode = hm(t.type, t.pendingProps, Te.current), aa = t, sa = !0, a = R, Sp(t.type) ? (um = a, R = lm(r.firstChild)) : R = a), Ic(e, t, t.pendingProps.children, n), Kc(e, t), e === null && (t.flags |= 4194304), t.child;
			case 5: return e === null && z && ((a = r = R) && (r = rm(r, t.type, t.pendingProps, sa), r === null ? a = !1 : (t.stateNode = r, aa = t, R = lm(r.firstChild), sa = !1, a = !0)), a || la(t)), ke(t), a = t.type, o = t.pendingProps, s = e === null ? null : e.memoizedProps, r = o.children, pp(a, o) ? r = null : s !== null && pp(a, s) && (t.flags |= 32), t.memoizedState !== null && (a = es(e, t, rs, null, null, n), sh._currentValue = a), Kc(e, t), Ic(e, t, r, n), t.child;
			case 6: return e === null && z && ((e = n = R) && (n = im(n, t.pendingProps, sa), n === null ? e = !1 : (t.stateNode = n, aa = t, R = null, e = !0)), e || la(t)), null;
			case 13: return el(e, t, n);
			case 4: return De(t, t.stateNode.containerInfo), r = t.pendingProps, e === null ? t.child = po(t, null, r, n) : Ic(e, t, r, n), t.child;
			case 11: return Lc(e, t, t.type, t.pendingProps, n);
			case 7: return r = t.pendingProps, Kc(e, t), Ic(e, t, r, n), t.child;
			case 8: return Ic(e, t, t.pendingProps.children, n), t.child;
			case 12: return Ic(e, t, t.pendingProps.children, n), t.child;
			case 10: return ul(e, t, n);
			case 9: return a = t.type._context, r = t.pendingProps.children, Ta(t), a = Ea(a), r = r(a), t.flags |= 1, Ic(e, t, r, n), t.child;
			case 14: return Rc(e, t, t.type, t.pendingProps, n);
			case 15: return zc(e, t, t.type, t.pendingProps, n);
			case 19: return ll(e, t, n);
			case 31: return Gc(e, t, n);
			case 22: return Bc(e, t, n, t.pendingProps);
			case 24: return Ta(t), r = Ea(Ma), e === null ? (a = qa(), a === null && (a = K, o = Na(), a.pooledCache = o, o.refCount++, o !== null && (a.pooledCacheLanes |= n), a = o), t.memoizedState = {
				parent: r,
				cache: a
			}, go(t), ya(t, Ma, a)) : ((e.lanes & n) !== 0 && (_o(e, t), wo(t, null, null, n), Co()), a = e.memoizedState, o = t.memoizedState, a.parent === r ? (r = o.cache, ya(t, Ma, r), r !== a.cache && Sa(t, [Ma], n, !0)) : (a = {
				parent: r,
				cache: r
			}, t.memoizedState = a, t.lanes === 0 && (t.memoizedState = t.updateQueue.baseState = a), ya(t, Ma, r))), Ic(e, t, t.pendingProps.children, n), t.child;
			case 30: return t.stateNode === null && (t.stateNode = {
				autoName: null,
				paired: null,
				clones: null,
				ref: null
			}), r = t.pendingProps, r.name != null && r.name !== "auto" ? t.flags |= e === null ? 18882560 : 18874368 : z && na(t), e !== null && e.memoizedProps.name !== r.name ? t.flags |= 4194816 : Kc(e, t), Ic(e, t, r.children, n), t.child;
			case 29: throw t.pendingProps;
		}
		throw Error(i(156, t.tag));
	}
	function hl(e) {
		e.flags |= 4;
	}
	function gl(e, t, n, r, i) {
		var a;
		if ((a = !!(e.mode & 32)) && (a = n === null ? Jm(t, r) : Jm(t, r) && (r.src !== n.src || r.srcSet !== n.srcSet)), a) {
			if (e.flags |= 16777216, (i & 335544128) === i) {
				if (e.stateNode.complete) e.flags |= 8192;
				else if (Ud()) e.flags |= 8192;
				else throw ro = $a, Za;
			}
		} else e.flags &= -16777217;
	}
	function _l(e, t) {
		if (t.type !== "stylesheet" || t.state.loading & 4) e.flags &= -16777217;
		else if (e.flags |= 16777216, !Ym(t)) {
			if (Ud()) e.flags |= 8192;
			else throw ro = $a, Za;
		}
	}
	function vl(e, t) {
		t !== null && (e.flags |= 4), e.flags & 16384 && (t = e.tag === 22 ? 536870912 : pt(), e.lanes |= t, dd |= t);
	}
	function yl(e, t) {
		if (!z) switch (e.tailMode) {
			case "visible": break;
			case "collapsed":
				for (var n = e.tail, r = null; n !== null;) n.alternate !== null && (r = n), n = n.sibling;
				r === null ? t || e.tail === null ? e.tail = null : e.tail.sibling = null : r.sibling = null;
				break;
			default:
				for (t = e.tail, n = null; t !== null;) t.alternate !== null && (n = t), t = t.sibling;
				n === null ? e.tail = null : n.sibling = null;
		}
	}
	function H(e) {
		var t = e.alternate !== null && e.alternate.child === e.child, n = 0, r = 0;
		if (t) for (var i = e.child; i !== null;) n |= i.lanes | i.childLanes, r |= i.subtreeFlags & 1206910976, r |= i.flags & 1206910976, i.return = e, i = i.sibling;
		else for (i = e.child; i !== null;) n |= i.lanes | i.childLanes, r |= i.subtreeFlags, r |= i.flags, i.return = e, i = i.sibling;
		return e.subtreeFlags |= r, e.childLanes = n, t;
	}
	function bl(e, t, n) {
		var r = t.pendingProps;
		switch (ra(t), t.tag) {
			case 16:
			case 15:
			case 0:
			case 11:
			case 7:
			case 8:
			case 12:
			case 9:
			case 14: return H(t), null;
			case 1: return H(t), null;
			case 3: return n = t.stateNode, r = null, e !== null && (r = e.memoizedState.cache), t.memoizedState.cache !== r && (t.flags |= 2048), ba(Ma), Oe(), n.pendingContext && (n.context = n.pendingContext, n.pendingContext = null), (e === null || e.child === null) && (fa(t) ? hl(t) : e === null || e.memoizedState.isDehydrated && !(t.flags & 256) || (t.flags |= 1024, ma())), H(t), null;
			case 26:
				var a = t.type, o = t.memoizedState;
				return e === null ? (hl(t), o === null ? (H(t), gl(t, a, null, r, n)) : (H(t), _l(t, o))) : o ? o === e.memoizedState ? (H(t), t.flags &= -16777217) : (hl(t), H(t), _l(t, o)) : (e = e.memoizedProps, e !== r && hl(t), H(t), gl(t, a, e, r, n)), null;
			case 27:
				if (Ae(t), n = Te.current, a = t.type, e !== null && t.stateNode != null) e.memoizedProps !== r && hl(t);
				else {
					if (!r) {
						if (t.stateNode === null) throw Error(i(166));
						return H(t), t.subtreeFlags &= -33554433, null;
					}
					e = Ce.current, fa(t) ? ua(t, e) : (e = hm(a, r, n), t.stateNode = e, hl(t));
				}
				return H(t), t.subtreeFlags &= -33554433, null;
			case 5:
				if (Ae(t), a = t.type, e !== null && t.stateNode != null) e.memoizedProps !== r && hl(t);
				else {
					if (!r) {
						if (t.stateNode === null) throw Error(i(166));
						return H(t), t.subtreeFlags &= -33554433, null;
					}
					if (o = Ce.current, fa(t)) ua(t, o);
					else {
						var s = lp(Te.current);
						switch (o) {
							case 1:
								o = s.createElementNS("http://www.w3.org/2000/svg", a);
								break;
							case 2:
								o = s.createElementNS("http://www.w3.org/1998/Math/MathML", a);
								break;
							default: switch (a) {
								case "svg":
									o = s.createElementNS("http://www.w3.org/2000/svg", a);
									break;
								case "math":
									o = s.createElementNS("http://www.w3.org/1998/Math/MathML", a);
									break;
								case "script":
									o = s.createElement("div"), o.innerHTML = "<script><\/script>", o = o.removeChild(o.firstChild);
									break;
								case "select":
									o = typeof r.is == "string" ? s.createElement("select", { is: r.is }) : s.createElement("select"), r.multiple ? o.multiple = !0 : r.size && (o.size = r.size);
									break;
								default: o = typeof r.is == "string" ? s.createElement(a, { is: r.is }) : s.createElement(a);
							}
						}
						o[I] = t, o[Tt] = r;
						a: for (s = t.child; s !== null;) {
							if (s.tag === 5 || s.tag === 6) o.appendChild(s.stateNode);
							else if (s.tag !== 4 && s.tag !== 27 && s.child !== null) {
								s.child.return = s, s = s.child;
								continue;
							}
							if (s === t) break a;
							for (; s.sibling === null;) {
								if (s.return === null || s.return === t) break a;
								s = s.return;
							}
							s.sibling.return = s.return, s = s.sibling;
						}
						t.stateNode = o;
						a: switch (np(o, a, r), a) {
							case "button":
							case "input":
							case "select":
							case "textarea":
								r = !!r.autoFocus;
								break a;
							case "img":
								r = !0;
								break a;
							default: r = !1;
						}
						r && hl(t);
					}
				}
				return H(t), t.subtreeFlags &= -33554433, gl(t, t.type, e === null ? null : e.memoizedProps, t.pendingProps, n), null;
			case 6:
				if (e && t.stateNode != null) e.memoizedProps !== r && hl(t);
				else {
					if (typeof r != "string" && t.stateNode === null) throw Error(i(166));
					if (e = Te.current, fa(t)) {
						if (e = t.stateNode, n = t.memoizedProps, r = null, a = aa, a !== null) switch (a.tag) {
							case 27:
							case 5: r = a.memoizedProps;
						}
						e[I] = t, e = !!(e.nodeValue === n || r !== null && !0 === r.suppressHydrationWarning || ep(e.nodeValue, n)), e || la(t, !0);
					} else e = lp(e).createTextNode(r), e[I] = t, t.stateNode = e;
				}
				return H(t), null;
			case 31:
				if (n = t.memoizedState, e === null || e.memoizedState !== null) {
					if (r = fa(t), n !== null) {
						if (e === null) {
							if (!r) throw Error(i(318));
							if (e = t.memoizedState, e = e === null ? null : e.dehydrated, !e) throw Error(i(557));
							e[I] = t;
						} else pa(), !(t.flags & 128) && (t.memoizedState = null), t.flags |= 4;
						H(t), e = !1;
					} else n = ma(), e !== null && e.memoizedState !== null && (e.memoizedState.hydrationErrors = n), e = !0;
					if (!e) return t.flags & 256 ? (Ro(t), t) : (Ro(t), null);
					if (t.flags & 128) throw Error(i(558));
				}
				return H(t), null;
			case 13:
				if (r = t.memoizedState, e === null || e.memoizedState !== null && e.memoizedState.dehydrated !== null) {
					if (a = fa(t), r !== null && r.dehydrated !== null) {
						if (e === null) {
							if (!a) throw Error(i(318));
							if (a = t.memoizedState, a = a === null ? null : a.dehydrated, !a) throw Error(i(317));
							a[I] = t;
						} else pa(), !(t.flags & 128) && (t.memoizedState = null), t.flags |= 4;
						H(t), a = !1;
					} else a = ma(), e !== null && e.memoizedState !== null && (e.memoizedState.hydrationErrors = a), a = !0;
					if (!a) return t.flags & 256 ? (Ro(t), t) : (Ro(t), null);
				}
				return Ro(t), t.flags & 128 ? (t.lanes = n, t) : (n = r !== null, e = e !== null && e.memoizedState !== null, n && (r = t.child, a = null, r.alternate !== null && r.alternate.memoizedState !== null && r.alternate.memoizedState.cachePool !== null && (a = r.alternate.memoizedState.cachePool.pool), o = null, r.memoizedState !== null && r.memoizedState.cachePool !== null && (o = r.memoizedState.cachePool.pool), o !== a && (r.flags |= 2048)), n !== e && n && (t.child.flags |= 8192), vl(t, t.updateQueue), H(t), null);
			case 4: return Oe(), e === null && Wf(t.stateNode.containerInfo), t.flags |= 67108864, H(t), null;
			case 10: return ba(t.type), H(t), null;
			case 19:
				if (Vo(t), r = t.memoizedState, r === null) return H(t), null;
				if (a = !!(t.flags & 128), o = r.rendering, o === null) {
					if (a) yl(r, !1);
					else {
						if (od !== 0 || e !== null && e.flags & 128) for (e = t.child; e !== null;) {
							if (o = Ho(e), o !== null) {
								for (t.flags |= 128, yl(r, !1), e = o.updateQueue, t.updateQueue = e, vl(t, e), t.subtreeFlags = 0, e = n, n = t.child; n !== null;) Li(n, e), n = n.sibling;
								return Bo(t, zo.current & 1 | 2), z && ea(t, r.treeForkCount), t.child;
							}
							e = e.sibling;
						}
						r.tail !== null && Ue() > _d && (t.flags |= 128, a = !0, yl(r, !1), t.lanes = 4194304);
					}
				} else {
					if (!a) {
						if (e = Ho(o), e !== null) {
							if (t.flags |= 128, a = !0, e = e.updateQueue, t.updateQueue = e, vl(t, e), yl(r, !0), r.tail === null && r.tailMode !== "collapsed" && r.tailMode !== "visible" && !o.alternate && !z) return H(t), null;
						} else 2 * Ue() - r.renderingStartTime > _d && n !== 536870912 && (t.flags |= 128, a = !0, yl(r, !1), t.lanes = 4194304);
					}
					r.isBackwards ? (o.sibling = t.child, t.child = o) : (e = r.last, e === null ? t.child = o : e.sibling = o, r.last = o);
				}
				if (r.tail !== null) {
					e = r.tail;
					a: {
						for (n = e; n !== null;) {
							if (n.alternate !== null) {
								n = !1;
								break a;
							}
							n = n.sibling;
						}
						n = !0;
					}
					return r.rendering = e, r.tail = e.sibling, r.renderingStartTime = Ue(), e.sibling = null, o = zo.current, o = a ? o & 1 | 2 : o & 1, r.tailMode === "visible" || r.tailMode === "collapsed" || !n || z ? Bo(t, o) : (n = o, F(Mo, t), F(zo, n), No === null && (No = t)), z && ea(t, r.treeForkCount), e;
				}
				return H(t), null;
			case 22:
			case 23: return Ro(t), jo(), r = t.memoizedState !== null, e === null ? r && (t.flags |= 8192) : e.memoizedState !== null !== r && (t.flags |= 8192), r ? n & 536870912 && !(t.flags & 128) && (H(t), t.subtreeFlags & 6 && (t.flags |= 8192)) : H(t), n = t.updateQueue, n !== null && vl(t, n.retryQueue), n = null, e !== null && e.memoizedState !== null && e.memoizedState.cachePool !== null && (n = e.memoizedState.cachePool.pool), r = null, t.memoizedState !== null && t.memoizedState.cachePool !== null && (r = t.memoizedState.cachePool.pool), r !== n && (t.flags |= 2048), e !== null && Se(Ka), null;
			case 24: return n = null, e !== null && (n = e.memoizedState.cache), t.memoizedState.cache !== n && (t.flags |= 2048), ba(Ma), H(t), null;
			case 25: return null;
			case 30: return t.flags |= 33554432, H(t), null;
		}
		throw Error(i(156, t.tag));
	}
	function xl(e, t) {
		switch (ra(t), t.tag) {
			case 1: return e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 3: return ba(Ma), Oe(), e = t.flags, e & 65536 && !(e & 128) ? (t.flags = e & -65537 | 128, t) : null;
			case 26:
			case 27:
			case 5: return Ae(t), null;
			case 31:
				if (t.memoizedState !== null) {
					if (Ro(t), t.alternate === null) throw Error(i(340));
					pa();
				}
				return e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 13:
				if (Ro(t), e = t.memoizedState, e !== null && e.dehydrated !== null) {
					if (t.alternate === null) throw Error(i(340));
					pa();
				}
				return e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 19: return Vo(t), e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, e = t.memoizedState, e !== null && (e.rendering = null, e.tail = null), t.flags |= 4, t) : null;
			case 4: return Oe(), null;
			case 10: return ba(t.type), null;
			case 22:
			case 23: return Ro(t), jo(), e !== null && Se(Ka), e = t.flags, e & 65536 ? (t.flags = e & -65537 | 128, t) : null;
			case 24: return ba(Ma), null;
			case 25: return null;
			default: return null;
		}
	}
	function Sl(e, t) {
		switch (ra(t), t.tag) {
			case 3:
				ba(Ma), Oe();
				break;
			case 26:
			case 27:
			case 5:
				Ae(t);
				break;
			case 4:
				Oe();
				break;
			case 31:
				t.memoizedState !== null && Ro(t);
				break;
			case 13:
				Ro(t);
				break;
			case 19:
				Vo(t);
				break;
			case 10:
				ba(t.type);
				break;
			case 22:
			case 23:
				Ro(t), jo(), e !== null && Se(Ka);
				break;
			case 24: ba(Ma);
		}
	}
	function Cl(e, t) {
		try {
			var n = t.updateQueue, r = n === null ? null : n.lastEffect;
			if (r !== null) {
				var i = r.next;
				n = i;
				do {
					if ((n.tag & e) === e) {
						r = void 0;
						var a = n.create, o = n.inst;
						r = a(), o.destroy = r;
					}
					n = n.next;
				} while (n !== i);
			}
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function wl(e, t, n) {
		try {
			var r = t.updateQueue, i = r === null ? null : r.lastEffect;
			if (i !== null) {
				var a = i.next;
				r = a;
				do {
					if ((r.tag & e) === e) {
						var o = r.inst, s = o.destroy;
						if (s !== void 0) {
							o.destroy = void 0, i = t;
							var c = n, l = s;
							try {
								l();
							} catch (e) {
								Z(i, c, e);
							}
						}
					}
					r = r.next;
				} while (r !== a);
			}
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function Tl(e) {
		var t = e.updateQueue;
		if (t !== null) {
			var n = e.stateNode;
			try {
				Eo(t, n);
			} catch (t) {
				Z(e, e.return, t);
			}
		}
	}
	function El(e, t, n) {
		n.props = wc(e.type, e.memoizedProps), n.state = e.memoizedState;
		try {
			n.componentWillUnmount();
		} catch (n) {
			Z(e, t, n);
		}
	}
	function Dl(e, t) {
		try {
			var n = e.ref;
			if (n !== null) {
				switch (e.tag) {
					case 26:
					case 27:
					case 5:
						var r = e.stateNode;
						break;
					case 30:
						var i = e.stateNode, a = yi(e.memoizedProps, i);
						(i.ref === null || i.ref.name !== a) && (i.ref = Pp(a)), r = i.ref;
						break;
					case 7:
						if (e.stateNode === null) {
							var o = new Fp(e);
							h(e.child, !1, Qp, o, void 0, void 0), e.stateNode = o;
						}
						r = e.stateNode;
						break;
					default: r = e.stateNode;
				}
				typeof n == "function" ? e.refCleanup = n(r) : n.current = r;
			}
		} catch (n) {
			Z(e, t, n);
		}
	}
	function Ol(e, t) {
		var n = e.ref, r = e.refCleanup;
		if (n !== null) {
			if (typeof r == "function") try {
				r();
			} catch (n) {
				Z(e, t, n);
			} finally {
				e.refCleanup = null, e = e.alternate, e != null && (e.refCleanup = null);
			}
			else if (typeof n == "function") try {
				n(null);
			} catch (n) {
				Z(e, t, n);
			}
			else n.current = null;
		}
	}
	function kl(e, t) {
		if ((e.tag === 5 || e.tag === 27 || e.tag === 6) && e.alternate === null && t !== null) for (var n = 0; n < t.length; n++) em(e.stateNode, t[n]);
	}
	function Al(e) {
		for (var t = e.return; t !== null && (Nl(t) && em(e.stateNode, t.stateNode), !Ml(t));) t = t.return;
	}
	function jl(e) {
		for (var t = e.return; t !== null && (Nl(t) && tm(e.stateNode, t.stateNode), !Ml(t));) t = t.return;
	}
	function Ml(e) {
		return e.tag === 5 || e.tag === 3 || e.tag === 27;
	}
	function Nl(e) {
		return e && e.tag === 7 && e.stateNode !== null;
	}
	function Pl(e) {
		var t = e.type, n = e.memoizedProps, r = e.stateNode;
		try {
			a: switch (t) {
				case "button":
				case "input":
				case "select":
				case "textarea":
					n.autoFocus && r.focus();
					break a;
				case "img": n.src ? r.src = n.src : n.srcSet && (r.srcset = n.srcSet);
			}
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	function Fl(e, t, n) {
		try {
			var r = e.stateNode;
			ip(r, e.type, n, t), r[Tt] = t;
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	function Il(e) {
		return e.tag === 5 || e.tag === 3 || e.tag === 26 || e.tag === 27 && Sp(e.type) || e.tag === 4;
	}
	function Ll(e) {
		a: for (;;) {
			for (; e.sibling === null;) {
				if (e.return === null || Il(e.return)) return null;
				e = e.return;
			}
			for (e.sibling.return = e.return, e = e.sibling; e.tag !== 5 && e.tag !== 6 && e.tag !== 18;) {
				if (e.tag === 27 && Sp(e.type) || e.flags & 2 || e.child === null || e.tag === 4) continue a;
				e.child.return = e, e = e.child;
			}
			if (!(e.flags & 2)) return e.stateNode;
		}
	}
	function Rl(e, t, n, r) {
		var i = e.tag;
		if (i === 5 || i === 6) i = e.stateNode, t ? (n.nodeType === 9 ? n.body : n.nodeName === "HTML" ? n.ownerDocument.body : n).insertBefore(i, t) : (t = n.nodeType === 9 ? n.body : n.nodeName === "HTML" ? n.ownerDocument.body : n, t.appendChild(i), n = n._reactRootContainer, n != null || t.onclick !== null || (t.onclick = bn)), kl(e, r), L = !0;
		else if (i !== 4 && (i === 27 && (kl(e, r), r = null, Sp(e.type) && (n = e.stateNode, t = null)), e = e.child, e !== null)) for (Rl(e, t, n, r), e = e.sibling; e !== null;) Rl(e, t, n, r), e = e.sibling;
	}
	function zl(e, t, n, r) {
		var i = e.tag;
		if (i === 5 || i === 6) i = e.stateNode, t ? n.insertBefore(i, t) : n.appendChild(i), kl(e, r), L = !0;
		else if (i !== 4 && (i === 27 && (kl(e, r), r = null, Sp(e.type) && (n = e.stateNode)), e = e.child, e !== null)) for (zl(e, t, n, r), e = e.sibling; e !== null;) zl(e, t, n, r), e = e.sibling;
	}
	function Bl(e) {
		var t = e.stateNode, n = e.memoizedProps;
		try {
			for (var r = e.type, i = t.attributes; i.length;) t.removeAttributeNode(i[0]);
			np(t, r, n), t[I] = e, t[Tt] = n;
		} catch (t) {
			Z(e, e.return, t);
		}
	}
	var Vl = !1, Hl = null;
	function Ul(e) {
		(e.tag === 30 || e.subtreeFlags & 33554432) && (Vl = !0);
	}
	var Wl = null;
	function Gl() {
		var e = Wl;
		return Wl = null, e;
	}
	var Kl = 0;
	function ql(e, t, n, r, i) {
		return Kl = 0, Jl(e.child, t, n, r, i);
	}
	function Jl(e, t, n, r, i) {
		for (var a = !1; e !== null;) {
			if (e.tag === 5) {
				var o = e.stateNode;
				if (r !== null) {
					var s = Op(o);
					r.push(s), s.view && (a = !0);
				} else a || Op(o).view && (a = !0);
				Vl = !0, Tp(o, Kl === 0 ? t : t + "_" + Kl, n), Kl++;
			} else (e.tag !== 22 || e.memoizedState === null) && (e.tag === 30 && i || Jl(e.child, t, n, r, i) && (a = !0));
			e = e.sibling;
		}
		return a;
	}
	function Yl(e, t) {
		for (; e !== null;) e.tag === 5 ? Ep(e.stateNode, e.memoizedProps) : (e.tag !== 22 || e.memoizedState === null) && (e.tag === 30 && t || Yl(e.child, t)), e = e.sibling;
	}
	function Xl(e) {
		if (e.subtreeFlags & 18874368) for (e = e.child; e !== null;) {
			if ((e.tag !== 22 || e.memoizedState === null) && (Xl(e), e.tag === 30 && e.flags & 18874368 && e.stateNode.paired)) {
				var t = e.memoizedProps;
				if (t.name == null || t.name === "auto") throw Error(i(544));
				var n = t.name;
				t = xi(t.default, t.share), t !== "none" && (ql(e, n, t, null, !1) || Yl(e.child, !1));
			}
			e = e.sibling;
		}
	}
	function Zl(e, t) {
		if (e.tag === 30) {
			var n = e.stateNode, r = e.memoizedProps, i = yi(r, n), a = xi(r.default, n.paired ? r.share : r.enter);
			a === "none" ? Xl(e) : ql(e, i, a, null, !1) ? (Xl(e), n.paired || t || Nd(e, r.onEnter)) : Yl(e.child, !1);
		} else if (e.subtreeFlags & 33554432) for (e = e.child; e !== null;) Zl(e, t), e = e.sibling;
		else Xl(e);
	}
	function Ql(e) {
		if (Hl !== null && Hl.size !== 0) {
			var t = Hl;
			if (e.subtreeFlags & 18874368) for (e = e.child; e !== null;) {
				if (e.tag !== 22 || e.memoizedState === null) {
					if (e.tag === 30 && e.flags & 18874368) {
						var n = e.memoizedProps, r = n.name;
						if (r != null && r !== "auto") {
							var i = t.get(r);
							if (i !== void 0) {
								var a = xi(n.default, n.share);
								if (a !== "none" && (ql(e, r, a, null, !1) ? (a = e.stateNode, i.paired = a, a.paired = i, Nd(e, n.onShare)) : Yl(e.child, !1)), t.delete(r), t.size === 0) break;
							}
						}
					}
					Ql(e);
				}
				e = e.sibling;
			}
		}
	}
	function $l(e) {
		if (e.tag === 30) {
			var t = e.memoizedProps, n = yi(t, e.stateNode), r = Hl === null ? void 0 : Hl.get(n), i = xi(t.default, r === void 0 ? t.exit : t.share);
			i !== "none" && (ql(e, n, i, null, !1) ? r === void 0 ? Nd(e, t.onExit) : (i = e.stateNode, r.paired = i, i.paired = r, Hl.delete(n), Nd(e, t.onShare)) : Yl(e.child, !1)), Hl !== null && Ql(e);
		} else if (e.subtreeFlags & 33554432) for (e = e.child; e !== null;) $l(e), e = e.sibling;
		else Hl !== null && Ql(e);
	}
	function eu(e) {
		for (e = e.child; e !== null;) {
			if (e.tag === 30) {
				var t = e.memoizedProps, n = yi(t, e.stateNode);
				t = xi(t.default, t.update), e.flags &= -5, t !== "none" && ql(e, n, t, e.memoizedState = [], !1);
			} else e.subtreeFlags & 33554432 && eu(e);
			e = e.sibling;
		}
	}
	function tu(e) {
		if (e.subtreeFlags & 18874368) for (e = e.child; e !== null;) {
			if (e.tag !== 22 || e.memoizedState === null) {
				if (e.tag === 30 && e.flags & 18874368) {
					var t = e.stateNode;
					t.paired !== null && (t.paired = null, Yl(e.child, !1));
				}
				tu(e);
			}
			e = e.sibling;
		}
	}
	function nu(e) {
		if (e.tag === 30) e.stateNode.paired = null, Yl(e.child, !1), tu(e);
		else if (e.subtreeFlags & 33554432) for (e = e.child; e !== null;) nu(e), e = e.sibling;
		else tu(e);
	}
	function ru(e) {
		for (e = e.child; e !== null;) e.tag === 30 ? Yl(e.child, !1) : e.subtreeFlags & 33554432 && ru(e), e = e.sibling;
	}
	function iu(e, t, n, r, i, a, o) {
		for (var s = !1; t !== null;) {
			if (t.tag === 5) {
				var c = t.stateNode;
				if (a !== null && Kl < a.length) {
					var l = a[Kl], u = Op(c);
					(l.view || u.view) && (s = !0);
					var d;
					if (d = !(e.flags & 4)) {
						if (u.clip) d = !0;
						else {
							d = l.rect;
							var f = u.rect;
							d = d.y !== f.y || d.x !== f.x || d.height !== f.height || d.width !== f.width;
						}
					}
					d && (e.flags |= 4), u.abs ? u = !l.abs : (l = l.rect, u = u.rect, u = l.height !== u.height || l.width !== u.width), u && (e.flags |= 32);
				} else e.flags |= 32;
				e.flags & 4 && Tp(c, Kl === 0 ? n : n + "_" + Kl, i), s && e.flags & 4 || (Wl === null && (Wl = []), Wl.push(c, Kl === 0 ? r : r + "_" + Kl, t.memoizedProps)), Kl++;
			} else (t.tag !== 22 || t.memoizedState === null) && (t.tag === 30 && o ? e.flags |= t.flags & 32 : iu(e, t.child, n, r, i, a, o) && (s = !0));
			t = t.sibling;
		}
		return s;
	}
	function au(e, t) {
		for (e = e.child; e !== null;) {
			if (e.tag === 30) {
				var n = e.memoizedProps, r = e.stateNode, i = yi(n, r), a = xi(n.default, n.update);
				if (t) {
					r = r.clones;
					var o = r === null ? null : r.map(kp);
				} else o = e.memoizedState, e.memoizedState = null;
				r = e;
				var s = e.child;
				Kl = 0, i = iu(r, s, i, i, a, o, !1), e.flags & 4 && i && (t || Nd(e, n.onUpdate));
			} else e.subtreeFlags & 33554432 && au(e, t);
			e = e.sibling;
		}
	}
	var ou = !1, U = !1, su = !1, cu = !1, lu = typeof WeakSet == "function" ? WeakSet : Set, uu = null, du = !1, fu = !1, pu = !1, mu = !1;
	function hu(e, t, n) {
		if (e = e.containerInfo, sp = gh, e = Yr(e), Xr(e)) {
			if ("selectionStart" in e) var r = {
				start: e.selectionStart,
				end: e.selectionEnd
			};
			else a: {
				r = (r = e.ownerDocument) && r.defaultView || window;
				var i = r.getSelection && r.getSelection();
				if (i && i.rangeCount !== 0) {
					r = i.anchorNode;
					var a = i.anchorOffset, o = i.focusNode;
					i = i.focusOffset;
					try {
						r.nodeType, o.nodeType;
					} catch {
						r = null;
						break a;
					}
					var s = 0, c = -1, l = -1, u = 0, d = 0, f = e, p = null;
					b: for (;;) {
						for (var m; f !== r || a !== 0 && f.nodeType !== 3 || (c = s + a), f !== o || i !== 0 && f.nodeType !== 3 || (l = s + i), f.nodeType === 3 && (s += f.nodeValue.length), (m = f.firstChild) !== null;) p = f, f = m;
						for (;;) {
							if (f === e) break b;
							if (p === r && ++u === a && (c = s), p === o && ++d === i && (l = s), (m = f.nextSibling) !== null) break;
							f = p, p = f.parentNode;
						}
						f = m;
					}
					r = c === -1 || l === -1 ? null : {
						start: c,
						end: l
					};
				} else r = null;
			}
			r ||= {
				start: 0,
				end: 0
			};
		} else r = null;
		for (cp = {
			focusedElem: e,
			selectionRange: r
		}, gh = !1, n = (n & 335544064) === n, uu = t, t = n ? 9270 : 1024; uu !== null;) {
			if (e = uu, n && (r = e.deletions, r !== null)) for (a = 0; a < r.length; a++) n && $l(r[a]);
			if (e.alternate === null && e.flags & 2) n && Ul(e), gu(n);
			else {
				if (e.tag === 22) {
					if (r = e.alternate, e.memoizedState !== null) {
						r !== null && r.memoizedState === null && n && $l(r), gu(n);
						continue;
					}
					if (r !== null && r.memoizedState !== null) {
						n && Ul(e), gu(n);
						continue;
					}
				}
				r = e.child, (e.subtreeFlags & t) !== 0 && r !== null ? (r.return = e, uu = r) : (n && eu(e), gu(n));
			}
		}
		Hl = null;
	}
	function gu(e) {
		for (; uu !== null;) {
			var t = uu, n = e, r = t.alternate, a = t.flags;
			switch (t.tag) {
				case 0:
				case 11:
				case 15: break;
				case 1:
					if (a & 1024 && r !== null) {
						n = void 0, a = r.memoizedProps, r = r.memoizedState;
						var o = t.stateNode;
						try {
							var s = wc(t.type, a);
							n = o.getSnapshotBeforeUpdate(s, r), o.__reactInternalSnapshotBeforeUpdate = n;
						} catch (e) {
							Z(t, t.return, e);
						}
					}
					break;
				case 3:
					if (a & 1024) {
						if (r = t.stateNode.containerInfo, n = r.nodeType, n === 9) nm(r);
						else if (n === 1) switch (r.nodeName) {
							case "HEAD":
							case "HTML":
							case "BODY":
								nm(r);
								break;
							default: r.textContent = "";
						}
					}
					break;
				case 5:
				case 26:
				case 27:
				case 6:
				case 4:
				case 17: break;
				case 30:
					n && r !== null && (n = yi(r.memoizedProps, r.stateNode), a = t.memoizedProps, a = xi(a.default, a.update), a !== "none" && ql(r, n, a, r.memoizedState = [], !0));
					break;
				default: if (a & 1024) throw Error(i(163));
			}
			if (r = t.sibling, r !== null) {
				r.return = t.return, uu = r;
				break;
			}
			uu = t.return;
		}
	}
	function _u(e, t, n) {
		var r = n.flags;
		switch (n.tag) {
			case 0:
			case 11:
			case 15:
				Iu(e, n), r & 4 && Cl(5, n);
				break;
			case 1:
				if (Iu(e, n), r & 4) {
					if (e = n.stateNode, t === null) try {
						e.componentDidMount();
					} catch (e) {
						Z(n, n.return, e);
					}
					else {
						var i = wc(n.type, t.memoizedProps);
						t = t.memoizedState;
						try {
							e.componentDidUpdate(i, t, e.__reactInternalSnapshotBeforeUpdate);
						} catch (e) {
							Z(n, n.return, e);
						}
					}
				}
				r & 64 && Tl(n), r & 512 && Dl(n, n.return);
				break;
			case 3:
				if (Iu(e, n), r & 64 && (e = n.updateQueue, e !== null)) {
					if (t = null, n.child !== null) switch (n.child.tag) {
						case 27:
						case 5:
							t = n.child.stateNode;
							break;
						case 1: t = n.child.stateNode;
					}
					try {
						Eo(e, t);
					} catch (e) {
						Z(n, n.return, e);
					}
				}
				break;
			case 27: t === null && r & 4 && Bl(n);
			case 26:
			case 5:
				Iu(e, n), t === null && r & 4 && Pl(n), r & 512 && Dl(n, n.return);
				break;
			case 12:
				Iu(e, n);
				break;
			case 31:
				Iu(e, n), r & 4 && Tu(e, n);
				break;
			case 13:
				Iu(e, n), r & 4 && Eu(e, n), r & 64 && (e = n.memoizedState, e !== null && (e = e.dehydrated, e !== null && (n = _f.bind(null, n), cm(e, n))));
				break;
			case 22:
				if (r = n.memoizedState !== null || ou, !r) {
					var a = t !== null && t.memoizedState !== null || U;
					t = ou, i = U, ou = r, (U = a) && !i ? (r = 2, n.subtreeFlags & 8772 && (r |= 1), Ru(e, n, r)) : Iu(e, n), ou = t, U = i;
				}
				break;
			case 30:
				Iu(e, n), r & 512 && Dl(n, n.return);
				break;
			case 7: r & 512 && Dl(n, n.return);
			default: Iu(e, n);
		}
	}
	function vu(e, t) {
		for (e = e.child; e !== null;) yu(e, t), e = e.sibling;
	}
	function yu(e, t) {
		switch (e.tag) {
			case 5:
			case 26:
				try {
					var n = e.stateNode;
					if (t) {
						var r = n.style;
						typeof r.setProperty == "function" ? r.setProperty("display", "none", "important") : r.display = "none";
					} else {
						var i = e.stateNode, a = e.memoizedProps.style, o = a != null && a.hasOwnProperty("display") ? a.display : null;
						i.style.display = o == null || typeof o == "boolean" ? "" : ("" + o).trim();
					}
				} catch (t) {
					Z(e, e.return, t);
				}
				bu(e, t);
				break;
			case 6:
				try {
					e.stateNode.nodeValue = t ? "" : e.memoizedProps, L = !0;
				} catch (t) {
					Z(e, e.return, t);
				}
				break;
			case 18:
				try {
					var s = e.stateNode;
					t ? wp(s, !0) : wp(e.stateNode, !1);
				} catch (t) {
					Z(e, e.return, t);
				}
				break;
			case 22:
			case 23:
				e.memoizedState === null && vu(e, t);
				break;
			default: vu(e, t);
		}
	}
	function bu(e, t) {
		if (e.subtreeFlags & 67108864) for (e = e.child; e !== null;) {
			a: {
				var n = e, r = t;
				switch (n.tag) {
					case 4:
						yu(n, r);
						break a;
					case 22:
						n.memoizedState === null && bu(n, r);
						break a;
					default: bu(n, r);
				}
			}
			e = e.sibling;
		}
	}
	function xu(e) {
		var t = e.alternate;
		t !== null && (e.alternate = null, xu(t)), e.child = null, e.deletions = null, e.sibling = null, e.tag === 5 && (t = e.stateNode, t !== null && Nt(t)), e.stateNode = null, e.return = null, e.dependencies = null, e.memoizedProps = null, e.memoizedState = null, e.pendingProps = null, e.stateNode = null, e.updateQueue = null;
	}
	var W = null, Su = !1;
	function Cu(e, t, n) {
		for (n = n.child; n !== null;) wu(e, t, n), n = n.sibling;
	}
	function wu(e, t, n) {
		if ($e && typeof $e.onCommitFiberUnmount == "function") try {
			$e.onCommitFiberUnmount(Qe, n);
		} catch {}
		switch (n.tag) {
			case 26:
				U || Ol(n, t), Cu(e, t, n), n.memoizedState ? n.memoizedState.count-- : n.stateNode && !U && (n = n.stateNode, n.parentNode.removeChild(n));
				break;
			case 27:
				U || Ol(n, t), jl(n);
				var r = W, i = Su;
				Sp(n.type) && (W = n.stateNode, Su = !1), Cu(e, t, n), gm(n.stateNode, n.type, n.memoizedProps), W = r, Su = i;
				break;
			case 5: U || Ol(n, t), jl(n);
			case 6:
				if (n.tag === 6 && jl(n), r = W, i = Su, W = null, Cu(e, t, n), W = r, Su = i, W !== null) {
					if (Su) try {
						(W.nodeType === 9 ? W.body : W.nodeName === "HTML" ? W.ownerDocument.body : W).removeChild(n.stateNode), L = !0;
					} catch (e) {
						Z(n, t, e);
					}
					else try {
						W.removeChild(n.stateNode), L = !0;
					} catch (e) {
						Z(n, t, e);
					}
				}
				break;
			case 18:
				W !== null && (Su ? (e = W, Cp(e.nodeType === 9 ? e.body : e.nodeName === "HTML" ? e.ownerDocument.body : e, n.stateNode), Hh(e)) : Cp(W, n.stateNode));
				break;
			case 4:
				r = W, i = Su, W = n.stateNode.containerInfo, Su = !0, Cu(e, t, n), W = r, Su = i;
				break;
			case 0:
			case 11:
			case 14:
			case 15:
				wl(2, n, t), U || wl(4, n, t), Cu(e, t, n);
				break;
			case 1:
				U || (Ol(n, t), r = n.stateNode, typeof r.componentWillUnmount == "function" && El(n, t, r)), Cu(e, t, n);
				break;
			case 21:
				Cu(e, t, n);
				break;
			case 22:
				U = (r = U) || n.memoizedState !== null, Cu(e, t, n), U = r;
				break;
			case 30:
				Ol(n, t), Cu(e, t, n);
				break;
			case 7:
				U || Ol(n, t), Cu(e, t, n);
				break;
			default: Cu(e, t, n);
		}
	}
	function Tu(e, t) {
		if (t.memoizedState === null && (e = t.alternate, e !== null && (e = e.memoizedState, e !== null))) {
			e = e.dehydrated;
			try {
				Hh(e);
			} catch (e) {
				Z(t, t.return, e);
			}
		}
	}
	function Eu(e, t) {
		if (t.memoizedState === null && (e = t.alternate, e !== null && (e = e.memoizedState, e !== null && (e = e.dehydrated, e !== null)))) try {
			Hh(e);
		} catch (e) {
			Z(t, t.return, e);
		}
	}
	function Du(e) {
		switch (e.tag) {
			case 31:
			case 13:
			case 19:
				var t = e.stateNode;
				return t === null && (t = e.stateNode = new lu()), t;
			case 22: return e = e.stateNode, t = e._retryCache, t === null && (t = e._retryCache = new lu()), t;
			default: throw Error(i(435, e.tag));
		}
	}
	function Ou(e, t) {
		var n = Du(e);
		t.forEach(function(t) {
			if (!n.has(t)) {
				n.add(t);
				var r = vf.bind(null, e, t);
				t.then(r, r);
			}
		});
	}
	function ku(e, t, n) {
		var r = t.deletions;
		if (r !== null) for (var a = 0; a < r.length; a++) {
			var o = r[a], s = e, c = t, l = c;
			a: for (; l !== null;) {
				switch (l.tag) {
					case 27:
						if (Sp(l.type)) {
							W = l.stateNode, Su = !1;
							break a;
						}
						break;
					case 5:
						W = l.stateNode, Su = !1;
						break a;
					case 3:
					case 4:
						W = l.stateNode.containerInfo, Su = !0;
						break a;
				}
				l = l.return;
			}
			if (W === null) throw Error(i(160));
			wu(s, c, o), W = null, Su = !1, s = o.alternate, s !== null && (s.return = null), o.return = null;
		}
		if (t.subtreeFlags & 13886) for (t = t.child; t !== null;) ju(t, e, n), t = t.sibling;
	}
	var Au = null;
	function ju(e, t, n) {
		var r = e.alternate, a = e.flags;
		switch (e.tag) {
			case 0:
			case 11:
			case 14:
			case 15:
				if (a & 4 && (r = e.updateQueue, r = r === null ? null : r.events, r !== null)) for (var o = 0; o < r.length; o++) {
					var s = r[o];
					s.ref.impl = s.nextImpl;
				}
				ku(t, e, n), Mu(e), a & 4 && (wl(3, e, e.return), Cl(3, e), wl(5, e, e.return));
				break;
			case 1:
				ku(t, e, n), Mu(e), a & 512 && (U || r === null || Ol(r, r.return)), a & 64 && ou && (e = e.updateQueue, e !== null && (t = e.callbacks, t !== null && (n = e.shared.hiddenCallbacks, e.shared.hiddenCallbacks = n === null ? t : n.concat(t))));
				break;
			case 26:
				if (o = Au, ku(t, e, n), Mu(e), a & 512 && (U || r === null || Ol(r, r.return)), a & 4) {
					if (a = r === null ? null : r.memoizedState, n = e.memoizedState, r === null) {
						if (n === null) {
							if (e.stateNode === null) {
								if (ou) e.stateNode = fp(e.type, e.memoizedProps, t.containerInfo, e);
								else {
									a: {
										t = e.type, n = e.memoizedProps, a = o.ownerDocument || o;
										b: switch (t) {
											case "title":
												r = a.getElementsByTagName("title")[0], (!r || r[jt] || r[I] || r.namespaceURI === "http://www.w3.org/2000/svg" || r.hasAttribute("itemprop")) && (r = a.createElement(t), a.head.insertBefore(r, a.querySelector("head > title"))), np(r, t, n), r[I] = e, Rt(r), t = r;
												break a;
											case "link":
												if (o = Gm("link", "href", a).get(t + (n.href || ""))) {
													for (s = 0; s < o.length; s++) if (r = o[s], r.getAttribute("href") === (n.href == null || n.href === "" ? null : n.href) && r.getAttribute("rel") === (n.rel == null ? null : n.rel) && r.getAttribute("title") === (n.title == null ? null : n.title) && r.getAttribute("crossorigin") === (n.crossOrigin == null ? null : n.crossOrigin)) {
														o.splice(s, 1);
														break b;
													}
												}
												r = a.createElement(t), np(r, t, n), a.head.appendChild(r);
												break;
											case "meta":
												if (o = Gm("meta", "content", a).get(t + (n.content || ""))) {
													for (s = 0; s < o.length; s++) if (r = o[s], r.getAttribute("content") === (n.content == null ? null : "" + n.content) && r.getAttribute("name") === (n.name == null ? null : n.name) && r.getAttribute("property") === (n.property == null ? null : n.property) && r.getAttribute("http-equiv") === (n.httpEquiv == null ? null : n.httpEquiv) && r.getAttribute("charset") === (n.charSet == null ? null : n.charSet)) {
														o.splice(s, 1);
														break b;
													}
												}
												r = a.createElement(t), np(r, t, n), a.head.appendChild(r);
												break;
											default: throw Error(i(468, t));
										}
										r[I] = e, Rt(r), t = r;
									}
									e.stateNode = t;
								}
							} else ou || Km(o, e.type, e.stateNode);
						} else e.stateNode = Bm(o, n, e.memoizedProps);
					} else a === n ? n === null && e.stateNode !== null && Fl(e, e.memoizedProps, r.memoizedProps) : (a === null ? (t = r.stateNode, t === null || U || t.parentNode.removeChild(t)) : a.count--, n === null ? ou || Km(o, e.type, e.stateNode) : Bm(o, n, e.memoizedProps));
				}
				break;
			case 27:
				ku(t, e, n), Mu(e), a & 512 && (U || r === null || Ol(r, r.return)), r !== null && a & 4 && Fl(e, e.memoizedProps, r.memoizedProps);
				break;
			case 5:
				if (o = su, su = !1, ku(t, e, n), su = o, Mu(e), a & 512 && (U || r === null || Ol(r, r.return)), e.flags & 32) {
					t = e.stateNode;
					try {
						fn(t, ""), L = !0;
					} catch (t) {
						Z(e, e.return, t);
					}
				}
				a & 4 && e.stateNode != null && (t = e.memoizedProps, Fl(e, t, r === null ? t : r.memoizedProps)), a & 1024 && (cu = !0);
				break;
			case 6:
				if (ku(t, e, n), Mu(e), a & 4) {
					if (e.stateNode === null) throw Error(i(162));
					t = e.memoizedProps, n = e.stateNode;
					try {
						n.nodeValue = t, L = !0;
					} catch (t) {
						Z(e, e.return, t);
					}
				}
				break;
			case 3:
				if (L = !1, Wm = null, o = Au, Au = bm(t.containerInfo), ku(t, e, n), Au = o, Mu(e), a & 4 && r !== null && r.memoizedState.isDehydrated) try {
					Hh(t.containerInfo);
				} catch (t) {
					Z(e, e.return, t);
				}
				cu && (cu = !1, Nu(e)), L = !1;
				break;
			case 4:
				a = su, su = ou, r = Jt(), o = Au, Au = bm(e.stateNode.containerInfo), ku(t, e, n), Mu(e), Au = o, L && fu && (pu = !0), L = r, su = a;
				break;
			case 12:
				ku(t, e, n), Mu(e);
				break;
			case 31:
				ku(t, e, n), Mu(e), a & 4 && (t = e.updateQueue, t !== null && (e.updateQueue = null, Ou(e, t)));
				break;
			case 13:
				ku(t, e, n), Mu(e), e.child.flags & 8192 && e.memoizedState !== null != (r !== null && r.memoizedState !== null) && (hd = Ue()), a & 4 && (t = e.updateQueue, t !== null && (e.updateQueue = null, Ou(e, t)));
				break;
			case 22:
				o = e.memoizedState !== null, s = r !== null && r.memoizedState !== null;
				var c = ou, l = U, u = su;
				ou = c || o, su = u || o, U = l || s, ku(t, e, n), U = l, su = u, ou = c, Mu(e), a & 8192 && (t = e.stateNode, t._visibility = o ? t._visibility & -2 : t._visibility | 1, !o || r === null || s || ou || U || (t = s || U, n = ou, r = U, ou = o || ou, U = t, Lu(e, 2), ou = n, U = r), !o && su || vu(e, o)), a & 4 && (t = e.updateQueue, t !== null && (n = t.retryQueue, n !== null && (t.retryQueue = null, Ou(e, n))));
				break;
			case 19:
				ku(t, e, n), Mu(e), a & 4 && (t = e.updateQueue, t !== null && (e.updateQueue = null, Ou(e, t)));
				break;
			case 30:
				a & 512 && (U || r === null || Ol(r, r.return)), a = Jt(), o = fu, s = (n & 335544064) === n, c = e.memoizedProps, fu = s && xi(c.default, c.update) !== "none", ku(t, e, n), Mu(e), s && r !== null && L && (e.flags |= 4), fu = o, L = a;
				break;
			case 21: break;
			case 7: a & 512 && (U || r === null || Ol(r, r.return)), r && r.stateNode !== null && (r.stateNode._fragmentFiber = e);
			default: ku(t, e, n), Mu(e);
		}
	}
	function Mu(e) {
		var t = e.flags;
		if (t & 2) {
			try {
				for (var n, r = e.return; r !== null;) {
					if (Il(r)) {
						n = r;
						break;
					}
					r = r.return;
				}
				r = null;
				for (var a = e.return; a !== null;) {
					if (Nl(a)) {
						var o = a.stateNode;
						r === null ? r = [o] : r.push(o);
					}
					if (Ml(a)) break;
					a = a.return;
				}
				var s = r;
				if (n == null) throw Error(i(160));
				switch (n.tag) {
					case 27:
						var c = n.stateNode;
						zl(e, Ll(e), c, s);
						break;
					case 5:
						var l = n.stateNode;
						n.flags & 32 && (fn(l, ""), n.flags &= -33), zl(e, Ll(e), l, s);
						break;
					case 3:
					case 4:
						var u = n.stateNode.containerInfo;
						Rl(e, Ll(e), u, s);
						break;
					default: throw Error(i(161));
				}
			} catch (t) {
				Z(e, e.return, t);
			}
			e.flags &= -3;
		}
		t & 4096 && (e.flags &= -4097);
	}
	function Nu(e) {
		if (e.subtreeFlags & 1024) for (e = e.child; e !== null;) {
			var t = e;
			Nu(t), t.tag === 5 && t.flags & 1024 && (t = t.stateNode, gh = !0, t.reset(), gh = !1), e = e.sibling;
		}
	}
	function Pu(e, t) {
		if (t.subtreeFlags & 9270) for (t = t.child; t !== null;) Fu(t, e), t = t.sibling;
		else au(t, !1);
	}
	function Fu(e, t) {
		var n = e.alternate;
		if (n === null) Zl(e, !1);
		else switch (e.tag) {
			case 3:
				if (mu = du = !1, Gl(), Pu(t, e), !du && !pu) {
					if (e = Wl, e !== null) for (var r = 0; r < e.length; r += 3) {
						n = e[r];
						var i = e[r + 1];
						Ep(n, e[r + 2]), n = n.ownerDocument.documentElement, n !== null && n.animate({
							opacity: [0, 0],
							pointerEvents: ["none", "none"]
						}, {
							duration: 0,
							fill: "forwards",
							pseudoElement: "::view-transition-group(" + i + ")"
						});
					}
					e = t.containerInfo, e = e.nodeType === 9 ? e.documentElement : e.ownerDocument.documentElement, e !== null && e.style.viewTransitionName === "" && (e.style.viewTransitionName = "none", e.animate({
						opacity: [0, 0],
						pointerEvents: ["none", "none"]
					}, {
						duration: 0,
						fill: "forwards",
						pseudoElement: "::view-transition-group(root)"
					}), e.animate({
						width: [0, 0],
						height: [0, 0]
					}, {
						duration: 0,
						fill: "forwards",
						pseudoElement: "::view-transition"
					})), mu = !0;
				}
				Wl = null;
				break;
			case 5:
				Pu(t, e);
				break;
			case 4:
				r = du, du = !1, Pu(t, e), du && (pu = !0), du = r;
				break;
			case 22:
				e.memoizedState === null && (n.memoizedState === null ? Pu(t, e) : Zl(e, !1));
				break;
			case 30:
				r = du, i = Gl(), du = !1, Pu(t, e), du && (e.flags |= 4);
				var a = e.memoizedProps, o = e.stateNode;
				t = yi(a, o), o = yi(n.memoizedProps, o);
				var s = xi(a.default, a.update);
				s === "none" ? t = !1 : (a = n.memoizedState, n.memoizedState = null, n = e.child, Kl = 0, t = iu(e, n, t, o, s, a, !0), Kl !== (a === null ? 0 : a.length) && (e.flags |= 32)), e.flags & 4 && t ? (Nd(e, e.memoizedProps.onUpdate), Wl = i) : i !== null && (i.push.apply(i, Wl), Wl = i), du = e.flags & 32 ? !0 : r;
				break;
			default: Pu(t, e);
		}
	}
	function Iu(e, t) {
		if (t.subtreeFlags & 8772) for (t = t.child; t !== null;) _u(e, t.alternate, t), t = t.sibling;
	}
	function Lu(e, t) {
		for (e = e.child; e !== null;) {
			var n = e, r = t;
			switch (n.tag) {
				case 0:
				case 11:
				case 14:
				case 15:
					wl(4, n, n.return), Lu(n, r);
					break;
				case 1:
					Ol(n, n.return);
					var i = n.stateNode;
					typeof i.componentWillUnmount == "function" && El(n, n.return, i), Lu(n, r);
					break;
				case 27: r & 2 && gm(n.stateNode, n.type, n.memoizedProps);
				case 5:
					Ol(n, n.return), n.tag !== 5 && n.tag !== 27 || jl(n), Lu(n, r);
					break;
				case 6:
					jl(n);
					break;
				case 26:
					Ol(n, n.return), i = n.stateNode, n.memoizedState !== null || i === null || U || i.parentNode.removeChild(i), Lu(n, r);
					break;
				case 22:
					n.memoizedState === null && Lu(n, r);
					break;
				case 30:
					Ol(n, n.return), Lu(n, r);
					break;
				case 7: Ol(n, n.return);
				default: Lu(n, r);
			}
			e = e.sibling;
		}
	}
	function Ru(e, t, n) {
		for (n = t.subtreeFlags & 8772 ? n : n & -2, t = t.child; t !== null;) {
			var r = t.alternate, i = e, a = t, o = a.flags, s = !!(n & 1);
			switch (a.tag) {
				case 0:
				case 11:
				case 15:
					Ru(i, a, n), Cl(4, a);
					break;
				case 1:
					if (Ru(i, a, n), r = a, i = r.stateNode, typeof i.componentDidMount == "function") try {
						i.componentDidMount();
					} catch (e) {
						Z(r, r.return, e);
					}
					if (r = a, i = r.updateQueue, i !== null) {
						var c = r.stateNode;
						try {
							var l = i.shared.hiddenCallbacks;
							if (l !== null) for (i.shared.hiddenCallbacks = null, i = 0; i < l.length; i++) To(l[i], c);
						} catch (e) {
							Z(r, r.return, e);
						}
					}
					s && o & 64 && Tl(a), Dl(a, a.return);
					break;
				case 27: n & 2 && Bl(a);
				case 5:
					a.tag !== 5 && a.tag !== 27 || Al(a), Ru(i, a, n), s && r === null && o & 4 && Pl(a), Dl(a, a.return);
					break;
				case 6:
					Al(a);
					break;
				case 26:
					c = a.stateNode, a.memoizedState !== null || c === null || ou || Km(bm(c.ownerDocument), a.type, c), Ru(i, a, n), s && r === null && o & 4 && Pl(a), Dl(a, a.return);
					break;
				case 12:
					Ru(i, a, n);
					break;
				case 31:
					Ru(i, a, n), s && o & 4 && Tu(i, a);
					break;
				case 13:
					Ru(i, a, n), s && o & 4 && Eu(i, a);
					break;
				case 22:
					a.memoizedState === null && Ru(i, a, n), Dl(a, a.return);
					break;
				case 30:
					Ru(i, a, n), Dl(a, a.return);
					break;
				case 7: Dl(a, a.return);
				default: Ru(i, a, n);
			}
			t = t.sibling;
		}
	}
	function zu(e, t) {
		var n = null;
		e !== null && e.memoizedState !== null && e.memoizedState.cachePool !== null && (n = e.memoizedState.cachePool.pool), e = null, t.memoizedState !== null && t.memoizedState.cachePool !== null && (e = t.memoizedState.cachePool.pool), e !== n && (e != null && e.refCount++, n != null && Pa(n));
	}
	function Bu(e, t) {
		e = null, t.alternate !== null && (e = t.alternate.memoizedState.cache), t = t.memoizedState.cache, t !== e && (t.refCount++, e != null && Pa(e));
	}
	function Vu(e, t, n, r) {
		var i = (n & 335544064) === n;
		if (t.subtreeFlags & (i ? 10262 : 10256)) for (t = t.child; t !== null;) Hu(e, t, n, r), t = t.sibling;
		else i && ru(t);
	}
	function Hu(e, t, n, r) {
		var i = (n & 335544064) === n;
		i && t.alternate === null && t.return !== null && t.return.alternate !== null && nu(t);
		var a = t.flags;
		switch (t.tag) {
			case 0:
			case 11:
			case 15:
				Vu(e, t, n, r), a & 2048 && Cl(9, t);
				break;
			case 1:
				Vu(e, t, n, r);
				break;
			case 3:
				Vu(e, t, n, r), i && mu && (e = e.containerInfo, e = e.nodeType === 9 ? e.body : e.nodeName === "HTML" ? e.ownerDocument.body : e, e.style.viewTransitionName === "root" && (e.style.viewTransitionName = ""), e = e.ownerDocument.documentElement, e !== null && e.style.viewTransitionName === "none" && (e.style.viewTransitionName = "")), a & 2048 && (a = null, t.alternate !== null && (a = t.alternate.memoizedState.cache), t = t.memoizedState.cache, t !== a && (t.refCount++, a != null && Pa(a)));
				break;
			case 12:
				if (a & 2048) {
					Vu(e, t, n, r), a = t.stateNode;
					try {
						var o = t.memoizedProps, s = o.id, c = o.onPostCommit;
						typeof c == "function" && c(s, t.alternate === null ? "mount" : "update", a.passiveEffectDuration, -0);
					} catch (e) {
						Z(t, t.return, e);
					}
				} else Vu(e, t, n, r);
				break;
			case 31:
				Vu(e, t, n, r);
				break;
			case 13:
				Vu(e, t, n, r);
				break;
			case 23: break;
			case 22:
				o = t.stateNode, s = t.alternate, t.memoizedState === null ? (i && s !== null && s.memoizedState !== null && nu(t), o._visibility & 2 ? Vu(e, t, n, r) : (o._visibility |= 2, Uu(e, t, n, r, !!(t.subtreeFlags & 10256) || !1))) : (i && s !== null && s.memoizedState === null && nu(s), o._visibility & 2 ? Vu(e, t, n, r) : Wu(e, t)), a & 2048 && zu(s, t);
				break;
			case 24:
				Vu(e, t, n, r), a & 2048 && Bu(t.alternate, t);
				break;
			case 30:
				i && (a = t.alternate, a !== null && (Yl(a.child, !0), Yl(t.child, !0))), Vu(e, t, n, r);
				break;
			default: Vu(e, t, n, r);
		}
	}
	function Uu(e, t, n, r, i) {
		for (i &&= !!(t.subtreeFlags & 10256) || !1, t = t.child; t !== null;) {
			var a = e, o = t, s = n, c = r, l = o.flags;
			switch (o.tag) {
				case 0:
				case 11:
				case 15:
					Uu(a, o, s, c, i), Cl(8, o);
					break;
				case 23: break;
				case 22:
					var u = o.stateNode;
					o.memoizedState === null ? (u._visibility |= 2, Uu(a, o, s, c, i)) : u._visibility & 2 ? Uu(a, o, s, c, i) : Wu(a, o), i && l & 2048 && zu(o.alternate, o);
					break;
				case 24:
					Uu(a, o, s, c, i), i && l & 2048 && Bu(o.alternate, o);
					break;
				default: Uu(a, o, s, c, i);
			}
			t = t.sibling;
		}
	}
	function Wu(e, t) {
		if (t.subtreeFlags & 10256) for (t = t.child; t !== null;) {
			var n = e, r = t, i = r.flags;
			switch (r.tag) {
				case 22:
					Wu(n, r), i & 2048 && zu(r.alternate, r);
					break;
				case 24:
					Wu(n, r), i & 2048 && Bu(r.alternate, r);
					break;
				default: Wu(n, r);
			}
			t = t.sibling;
		}
	}
	var Gu = 8192;
	function Ku(e, t, n) {
		if (e.subtreeFlags & Gu) for (e = e.child; e !== null;) qu(e, t, n), e = e.sibling;
	}
	function qu(e, t, n) {
		switch (e.tag) {
			case 26:
				Ku(e, t, n), e.flags & Gu && (e.memoizedState === null ? (e = e.stateNode, (t & 335544128) === t && Zm(n, e)) : Qm(n, Au, e.memoizedState, e.memoizedProps));
				break;
			case 5:
				Ku(e, t, n), e.flags & Gu && (e = e.stateNode, (t & 335544128) === t && Zm(n, e));
				break;
			case 3:
			case 4:
				var r = Au;
				Au = bm(e.stateNode.containerInfo), Ku(e, t, n), Au = r;
				break;
			case 22:
				e.memoizedState === null && (r = e.alternate, r !== null && r.memoizedState !== null ? (r = Gu, Gu = 16777216, Ku(e, t, n), Gu = r) : Ku(e, t, n));
				break;
			case 30:
				if ((e.flags & Gu) !== 0 && (r = e.memoizedProps.name, r != null && r !== "auto")) {
					var i = e.stateNode;
					i.paired = null, Hl === null && (Hl = /* @__PURE__ */ new Map()), Hl.set(r, i);
				}
				Ku(e, t, n);
				break;
			default: Ku(e, t, n);
		}
	}
	function Ju(e) {
		var t = e.alternate;
		if (t !== null && (e = t.child, e !== null)) {
			t.child = null;
			do
				t = e.sibling, e.sibling = null, e = t;
			while (e !== null);
		}
	}
	function Yu(e) {
		var t = e.deletions;
		if (e.flags & 16) {
			if (t !== null) for (var n = 0; n < t.length; n++) {
				var r = t[n];
				uu = r, Qu(r, e);
			}
			Ju(e);
		}
		if (e.subtreeFlags & 10256) for (e = e.child; e !== null;) Xu(e), e = e.sibling;
	}
	function Xu(e) {
		switch (e.tag) {
			case 0:
			case 11:
			case 15:
				Yu(e), e.flags & 2048 && wl(9, e, e.return);
				break;
			case 3:
				Yu(e);
				break;
			case 12:
				Yu(e);
				break;
			case 22:
				var t = e.stateNode;
				e.memoizedState !== null && t._visibility & 2 && (e.return === null || e.return.tag !== 13) ? (t._visibility &= -3, Zu(e)) : Yu(e);
				break;
			default: Yu(e);
		}
	}
	function Zu(e) {
		var t = e.deletions;
		if (e.flags & 16) {
			if (t !== null) for (var n = 0; n < t.length; n++) {
				var r = t[n];
				uu = r, Qu(r, e);
			}
			Ju(e);
		}
		for (e = e.child; e !== null;) {
			switch (t = e, t.tag) {
				case 0:
				case 11:
				case 15:
					wl(8, t, t.return), Zu(t);
					break;
				case 22:
					n = t.stateNode, n._visibility & 2 && (n._visibility &= -3, Zu(t));
					break;
				default: Zu(t);
			}
			e = e.sibling;
		}
	}
	function Qu(e, t) {
		for (; uu !== null;) {
			var n = uu;
			switch (n.tag) {
				case 0:
				case 11:
				case 15:
					wl(8, n, t);
					break;
				case 23:
				case 22:
					if (n.memoizedState !== null && n.memoizedState.cachePool !== null) {
						var r = n.memoizedState.cachePool.pool;
						r != null && r.refCount++;
					}
					break;
				case 24: Pa(n.memoizedState.cache);
			}
			if (r = n.child, r !== null) r.return = n, uu = r;
			else a: for (n = e; uu !== null;) {
				r = uu;
				var i = r.sibling, a = r.return;
				if (xu(r), r === n) {
					uu = null;
					break a;
				}
				if (i !== null) {
					i.return = a, uu = i;
					break a;
				}
				uu = a;
			}
		}
	}
	var $u = {
		getCacheForType: function(e) {
			var t = Ea(Ma), n = t.data.get(e);
			return n === void 0 && (n = e(), t.data.set(e, n)), n;
		},
		cacheSignal: function() {
			return Ea(Ma).controller.signal;
		}
	}, ed = typeof WeakMap == "function" ? WeakMap : Map, G = 0, K = null, q = null, J = 0, Y = 0, td = null, nd = !1, rd = !1, id = !1, ad = 0, od = 0, sd = 0, cd = 0, ld = 0, ud = 0, dd = 0, fd = null, pd = null, md = !1, hd = 0, gd = 0, _d = Infinity, vd = null, yd = null, X = 0, bd = null, xd = null, Sd = 0, Cd = 0, wd = null, Td = null, Ed = null, Dd = null, Od = null, kd = 0, Ad = null;
	function jd() {
		return G & 2 && J !== 0 ? J & -J : N.T === null ? St() : Pf();
	}
	function Md() {
		if (ud === 0) {
			if (!(J & 536870912) || z) {
				var e = ot;
				ot <<= 1, !(ot & 3932160) && (ot = 262144), ud = e;
			} else ud = 536870912;
		}
		return e = Mo.current, e !== null && (e.flags |= 32), ud;
	}
	function Nd(e, t) {
		if (t != null) {
			var n = e.stateNode, r = n.ref;
			r === null && (r = n.ref = Pp(yi(e.memoizedProps, n))), Dd === null && (Dd = []), Dd.push(t.bind(null, r));
		}
	}
	function Pd(e, t, n) {
		(e === K && (Y === 2 || Y === 9) || e.cancelPendingCommit !== null) && (Vd(e, 0), Rd(e, J, ud, !1)), ht(e, n), (!(G & 2) || e !== K) && (e === K && (!(G & 2) && (cd |= n), od === 4 && Rd(e, J, ud, !1)), Ef(e));
	}
	function Fd(e, t, n) {
		if (G & 6) throw Error(i(327));
		var r = !n && !(t & 127) && (t & e.expiredLanes) === 0 || ut(e, t), a = r ? Yd(e, t) : qd(e, t, !0), o = r;
		do {
			if (a === 0) {
				rd && !r && Rd(e, t, 0, !1);
				break;
			}
			if (n = e.current.alternate, o && !Ld(n)) {
				a = qd(e, t, !1), o = !1;
				continue;
			}
			if (a === 2) {
				if (o = t, e.errorRecoveryDisabledLanes & o) var s = 0;
				else s = e.pendingLanes & -536870913, s = s === 0 ? s & 536870912 ? 536870912 : 0 : s;
				if (s !== 0) {
					t = s;
					a: {
						var c = e;
						a = fd;
						var l = c.current.memoizedState.isDehydrated;
						if (l && (Vd(c, s).flags |= 256), s = qd(c, s, !1), s !== 2 && s !== 6) {
							if (id && !l) {
								c.errorRecoveryDisabledLanes |= o, cd |= o, a = 4;
								break a;
							}
							o = pd, pd = a, o !== null && (pd === null ? pd = o : pd.push.apply(pd, o));
						}
						a = s;
					}
					if (o = !1, a !== 2) continue;
				}
			}
			if (a === 1) {
				Vd(e, 0), Rd(e, t, 0, !0);
				break;
			}
			a: {
				switch (r = e, o = a, o) {
					case 0:
					case 1: throw Error(i(345));
					case 4: if ((t & 4194048) !== t && (t & 62914560) !== t) break;
					case 6:
						Rd(r, t, ud, !nd);
						break a;
					case 2:
						pd = null;
						break;
					case 3:
					case 5: break;
					default: throw Error(i(329));
				}
				if ((t & 62914560) === t && (a = hd + 300 - Ue(), 10 < a)) {
					if (Rd(r, t, ud, !nd), lt(r, 0, !0) !== 0) break a;
					Sd = t, r.timeoutHandle = gp(Id.bind(null, r, n, pd, vd, md, t, ud, cd, dd, nd, o, "Throttled", -0, 0), a);
					break a;
				}
				Id(r, n, pd, vd, md, t, ud, cd, dd, nd, o, null, -0, 0);
			}
			break;
		} while (1);
		Ef(e);
	}
	function Id(e, t, n, r, i, a, o, s, c, l, u, d, f, p) {
		e.timeoutHandle = -1;
		var m = t.subtreeFlags, h = (a & 335544064) === a;
		if (d = null, (h || m & 8192 || (m & 16785408) == 16785408) && (d = {
			stylesheets: null,
			count: 0,
			imgCount: 0,
			imgBytes: 0,
			suspenseyImages: [],
			waitingForImages: !0,
			waitingForViewTransition: !1,
			unsuspend: bn
		}, Hl = null, qu(t, a, d), h && (m = d, h = e.containerInfo, h = (h.nodeType === 9 ? h : h.ownerDocument).__reactViewTransition, h != null && (m.count++, m.waitingForViewTransition = !0, m = nh.bind(m), h.finished.then(m, m))), m = (a & 62914560) === a ? hd - Ue() : (a & 4194048) === a ? gd - Ue() : 0, m = eh(d, m), m !== null)) {
			Sd = a, e.cancelPendingCommit = m(nf.bind(null, e, t, a, n, r, i, o, s, c, l, u, d, null, f, p)), Rd(e, a, o, !l);
			return;
		}
		nf(e, t, a, n, r, i, o, s, c, l, u, d);
	}
	function Ld(e) {
		for (var t = e;;) {
			var n = t.tag;
			if ((n === 0 || n === 11 || n === 15) && t.flags & 16384 && (n = t.updateQueue, n !== null && (n = n.stores, n !== null))) for (var r = 0; r < n.length; r++) {
				var i = n[r], a = i.getSnapshot;
				i = i.value;
				try {
					if (!Ur(a(), i)) return !1;
				} catch {
					return !1;
				}
			}
			if (n = t.child, t.subtreeFlags & 16384 && n !== null) n.return = t, t = n;
			else {
				if (t === e) break;
				for (; t.sibling === null;) {
					if (t.return === null || t.return === e) return !0;
					t = t.return;
				}
				t.sibling.return = t.return, t = t.sibling;
			}
		}
		return !0;
	}
	function Rd(e, t, n, r) {
		t = dt(e, t), t &= ~ld, t &= ~cd, e.suspendedLanes |= t, e.pingedLanes &= ~t, r && (e.warmLanes |= t), r = e.expirationTimes;
		for (var i = t; 0 < i;) {
			var a = 31 - tt(i), o = 1 << a;
			r[a] = -1, i &= ~o;
		}
		n !== 0 && _t(e, n, t);
	}
	function zd() {
		return G & 6 ? !0 : (Df(0, !1), !1);
	}
	function Bd() {
		if (q !== null) {
			if (Y === 0) var e = q.return;
			else e = q, va = _a = null, os(e), oo = null, so = 0, e = q;
			for (; e !== null;) Sl(e.alternate, e), e = e.return;
			q = null;
		}
	}
	function Vd(e, t) {
		var n = e.timeoutHandle;
		return n !== -1 && (e.timeoutHandle = -1, _p(n)), n = e.cancelPendingCommit, n !== null && (e.cancelPendingCommit = null, n()), Sd = 0, Bd(), K = e, q = n = Ii(e.current, null), J = t, Y = 0, td = null, nd = !1, rd = ut(e, t), id = !1, dd = ud = ld = cd = sd = od = 0, pd = fd = null, md = !1, ad = dt(e, t), Ei(), n;
	}
	function Hd(e, t) {
		B = null, N.H = gc, t === Xa || t === Qa ? (t = io(), Y = 3) : t === Za ? (t = io(), Y = 4) : Y = t === Pc ? 8 : typeof t == "object" && t && typeof t.then == "function" ? 6 : 1, td = t, q === null && (od = 1, Oc(e, Wi(t, e.current)));
	}
	function Ud() {
		var e = Mo.current;
		return e === null ? !0 : (J & 4194048) === J ? No === null : (J & 62914560) === J || J & 536870912 ? e === No : !1;
	}
	function Wd() {
		var e = N.H;
		return N.H = gc, e === null ? gc : e;
	}
	function Gd() {
		var e = N.A;
		return N.A = $u, e;
	}
	function Kd() {
		od = 4, nd || (J & 4194048) !== J && Mo.current !== null || (rd = !0), !(sd & 134217727) && !(cd & 134217727) || K === null || Rd(K, J, ud, !1);
	}
	function qd(e, t, n) {
		var r = G;
		G |= 2;
		var i = Wd(), a = Gd();
		(K !== e || J !== t) && (vd = null, Vd(e, t)), t = !1;
		var o = od;
		a: do
			try {
				if (Y !== 0 && q !== null) {
					var s = q, c = td;
					switch (Y) {
						case 8:
							Bd(), o = 6;
							break a;
						case 3:
						case 2:
						case 9:
						case 6:
							Mo.current === null && (t = !0);
							var l = Y;
							if (Y = 0, td = null, $d(e, s, c, l), n && rd) {
								o = 0;
								break a;
							}
							break;
						default: l = Y, Y = 0, td = null, $d(e, s, c, l);
					}
				}
				Jd(), o = od;
				break;
			} catch (t) {
				Hd(e, t);
			}
		while (1);
		return t && e.shellSuspendCounter++, va = _a = null, G = r, N.H = i, N.A = a, q === null && (K = null, J = 0, Ei()), o;
	}
	function Jd() {
		for (; q !== null;) Zd(q);
	}
	function Yd(e, t) {
		var n = G;
		G |= 2;
		var r = Wd(), a = Gd();
		K !== e || J !== t ? (vd = null, _d = Ue() + 500, Vd(e, t)) : rd = ut(e, t);
		a: do
			try {
				if (Y !== 0 && q !== null) {
					t = q;
					var o = td;
					b: switch (Y) {
						case 1:
							Y = 0, td = null, $d(e, t, o, 1);
							break;
						case 2:
						case 9:
							if (eo(o)) {
								Y = 0, td = null, Qd(t);
								break;
							}
							t = function() {
								Y !== 2 && Y !== 9 || K !== e || (Y = 7), Ef(e);
							}, o.then(t, t);
							break a;
						case 3:
							Y = 7;
							break a;
						case 4:
							Y = 5;
							break a;
						case 7:
							eo(o) ? (Y = 0, td = null, Qd(t)) : (Y = 0, td = null, $d(e, t, o, 7));
							break;
						case 5:
							var s = null;
							switch (q.tag) {
								case 26: s = q.memoizedState;
								case 5:
								case 27:
									var c = q;
									if (s ? Ym(s) : c.stateNode.complete) {
										Y = 0, td = null;
										var l = c.sibling;
										if (l !== null) q = l;
										else {
											var u = c.return;
											u === null ? q = null : (q = u, ef(u));
										}
										break b;
									}
							}
							Y = 0, td = null, $d(e, t, o, 5);
							break;
						case 6:
							Y = 0, td = null, $d(e, t, o, 6);
							break;
						case 8:
							Bd(), od = 6;
							break a;
						default: throw Error(i(462));
					}
				}
				Xd();
				break;
			} catch (t) {
				Hd(e, t);
			}
		while (1);
		return va = _a = null, N.H = r, N.A = a, G = n, q === null ? (K = null, J = 0, Ei(), od) : 0;
	}
	function Xd() {
		for (; q !== null && !Ve();) Zd(q);
	}
	function Zd(e) {
		var t = ml(e.alternate, e, ad);
		e.memoizedProps = e.pendingProps, t === null ? ef(e) : q = t;
	}
	function Qd(e) {
		var t = e, n = t.alternate;
		switch (t.tag) {
			case 15:
			case 0:
				t = Jc(n, t, t.pendingProps, t.type, void 0, J);
				break;
			case 11:
				t = Jc(n, t, t.pendingProps, t.type.render, t.ref, J);
				break;
			case 5:
				os(t);
				var r = t;
				r === aa && (z ? (da(r), r.tag === 5 && r.stateNode != null && (R = r.stateNode)) : (da(r), z = !0));
			default: Sl(n, t), t = q = Li(t, ad), t = ml(n, t, ad);
		}
		e.memoizedProps = e.pendingProps, t === null ? ef(e) : q = t;
	}
	function $d(e, t, n, r) {
		va = _a = null, os(t), oo = null, so = 0;
		var i = t.return;
		try {
			if (Nc(e, i, t, n, J)) {
				od = 1, Oc(e, Wi(n, e.current)), q = null;
				return;
			}
		} catch (t) {
			if (i !== null) throw q = i, t;
			od = 1, Oc(e, Wi(n, e.current)), q = null;
			return;
		}
		t.flags & 32768 ? (z || r === 1 ? e = !0 : rd || J & 536870912 ? e = !1 : (nd = e = !0, (r === 2 || r === 9 || r === 3 || r === 6) && (r = Mo.current, r !== null && r.tag === 13 && (r.flags |= 16384))), tf(t, e)) : ef(t);
	}
	function ef(e) {
		var t = e;
		do {
			if (t.flags & 32768) {
				tf(t, nd);
				return;
			}
			e = t.return;
			var n = bl(t.alternate, t, ad);
			if (n !== null) {
				q = n;
				return;
			}
			if (t = t.sibling, t !== null) {
				q = t;
				return;
			}
			q = t = e;
		} while (t !== null);
		od === 0 && (od = 5);
	}
	function tf(e, t) {
		do {
			var n = xl(e.alternate, e);
			if (n !== null) {
				n.flags &= 32767, q = n;
				return;
			}
			if (n = e.return, n !== null && (n.flags |= 32768, n.subtreeFlags = 0, n.deletions = null), !t && (e = e.sibling, e !== null)) {
				q = e;
				return;
			}
			q = e = n;
		} while (e !== null);
		od = 6, q = null;
	}
	function nf(e, t, n, r, a, o, s, c, l, u, d, f) {
		e.cancelPendingCommit = null;
		do
			df();
		while (X !== 0);
		if (G & 6) throw Error(i(327));
		if (t !== null) {
			if (t === e.current) throw Error(i(177));
			e === K && (q = K = null, J = 0), xd = t, bd = e, Sd = n, wd = a, Td = r, rf(e, t, n, s, c, l, f);
		}
	}
	function rf(e, t, n, r, i, a, o) {
		var s = t.lanes | t.childLanes;
		if (Cd = s, s |= Ti, gt(e, n, s, r, i, a), Dd = null, (n & 335544064) === n ? (Od = La(e), r = 10262) : (Od = null, r = 10256), (t.subtreeFlags & r) !== 0 || (t.flags & r) !== 0 ? (e.callbackNode = null, e.callbackPriority = 0, yf(qe, function() {
			return ff(), null;
		})) : (e.callbackNode = null, e.callbackPriority = 0), Vl = !1, r = !!(t.flags & 13878), t.subtreeFlags & 13878 || r) {
			r = N.T, N.T = null, i = P.p, P.p = 2, a = G, G |= 4;
			try {
				hu(e, t, n);
			} finally {
				G = a, P.p = i, N.T = r;
			}
		}
		X = 1, Vl ? Ed = Mp(o, e.containerInfo, Od, sf, cf, of, lf, ff, af, null, null) : (sf(), cf(), lf());
	}
	function af(e) {
		if (X !== 0) {
			var t = bd.onRecoverableError;
			t(e, { componentStack: null });
		}
	}
	function of() {
		X === 3 && (X = 0, Fu(xd, bd), X = 4);
	}
	function sf() {
		if (X === 1) {
			X = 0;
			var e = bd, t = xd, n = Sd, r = !!(t.flags & 13878);
			if (t.subtreeFlags & 13878 || r) {
				r = N.T, N.T = null;
				var i = P.p;
				P.p = 2;
				var a = G;
				G |= 4;
				try {
					fu = pu = !1, ju(t, e, n), n = cp;
					var o = Yr(e.containerInfo), s = n.focusedElem, c = n.selectionRange;
					if (o !== s && s && s.ownerDocument && Jr(s.ownerDocument.documentElement, s)) {
						if (c !== null && Xr(s)) {
							var l = c.start, u = c.end;
							if (u === void 0 && (u = l), "selectionStart" in s) s.selectionStart = l, s.selectionEnd = Math.min(u, s.value.length);
							else {
								var d = s.ownerDocument || document, f = d && d.defaultView || window;
								if (f.getSelection) {
									var p = f.getSelection(), m = s.textContent.length, h = Math.min(c.start, m), g = c.end === void 0 ? h : Math.min(c.end, m);
									!p.extend && h > g && (o = g, g = h, h = o);
									var _ = qr(s, h), v = qr(s, g);
									if (_ && v && (p.rangeCount !== 1 || p.anchorNode !== _.node || p.anchorOffset !== _.offset || p.focusNode !== v.node || p.focusOffset !== v.offset)) {
										var y = d.createRange();
										y.setStart(_.node, _.offset), p.removeAllRanges(), h > g ? (p.addRange(y), p.extend(v.node, v.offset)) : (y.setEnd(v.node, v.offset), p.addRange(y));
									}
								}
							}
						}
						for (d = [], p = s; p = p.parentNode;) p.nodeType === 1 && d.push({
							element: p,
							left: p.scrollLeft,
							top: p.scrollTop
						});
						for (typeof s.focus == "function" && s.focus(), s = 0; s < d.length; s++) {
							var b = d[s];
							b.element.scrollLeft = b.left, b.element.scrollTop = b.top;
						}
					}
					gh = !!sp, cp = sp = null;
				} finally {
					G = a, P.p = i, N.T = r;
				}
			}
			e.current = t, X = 2;
		}
	}
	function cf() {
		if (X === 2) {
			X = 0;
			var e = bd, t = xd, n = !!(t.flags & 8772);
			if (t.subtreeFlags & 8772 || n) {
				n = N.T, N.T = null;
				var r = P.p;
				P.p = 2;
				var i = G;
				G |= 4;
				try {
					_u(e, t.alternate, t);
				} finally {
					G = i, P.p = r, N.T = n;
				}
			}
			X = 3;
		}
	}
	function lf() {
		if (X === 4 || X === 3) {
			X = 0;
			var e = Ed;
			Ed = null, He();
			var t = bd, n = xd, r = Sd, i = Td, a = (r & 335544064) === r ? 10262 : 10256;
			if ((n.subtreeFlags & a) !== 0 || (n.flags & a) !== 0 ? X = 5 : (X = 0, xd = bd = null, uf(t, t.pendingLanes)), a = t.pendingLanes, a === 0 && (yd = null), xt(r), n = n.stateNode, $e && typeof $e.onCommitFiberRoot == "function") try {
				$e.onCommitFiberRoot(Qe, n, void 0, (n.current.flags & 128) == 128);
			} catch {}
			if (i !== null) {
				n = N.T, a = P.p, P.p = 2, N.T = null;
				try {
					for (var o = t.onRecoverableError, s = 0; s < i.length; s++) {
						var c = i[s];
						o(c.value, { componentStack: c.stack });
					}
				} finally {
					N.T = n, P.p = a;
				}
			}
			if (i = Dd, o = Od, Od = null, i !== null && (Dd = null, o === null && (o = []), e !== null)) for (c = 0; c < i.length; c++) n = (0, i[c])(o), n !== void 0 && e.finished.finally(n);
			Sd & 3 && df(), Ef(t), a = t.pendingLanes, r & 261930 && a & 42 ? t === Ad ? kd++ : (kd = 0, Ad = t) : (kd = 0, Ad = null), Df(0, !1);
		}
	}
	function uf(e, t) {
		(e.pooledCacheLanes &= t) === 0 && (t = e.pooledCache, t != null && (e.pooledCache = null, Pa(t)));
	}
	function df() {
		return Ed !== null && (Ed.skipTransition(), Ed = null), sf(), cf(), lf(), ff();
	}
	function ff() {
		if (X !== 5) return !1;
		var e = bd, t = Cd;
		Cd = 0;
		var n = xt(Sd), r = N.T, a = P.p;
		try {
			P.p = 32 > n ? 32 : n, N.T = null, n = wd, wd = null;
			var o = bd, s = Sd;
			if (X = 0, xd = bd = null, Sd = 0, G & 6) throw Error(i(331));
			var c = G;
			if (G |= 4, Xu(o.current), Hu(o, o.current, s, n), G = c, Df(0, !1), $e && typeof $e.onPostCommitFiberRoot == "function") try {
				$e.onPostCommitFiberRoot(Qe, o);
			} catch {}
			return !0;
		} finally {
			P.p = a, N.T = r, uf(e, t);
		}
	}
	function pf(e, t, n) {
		t = Wi(n, t), t = Ac(e.stateNode, t, 2), e = yo(e, t, 2), e !== null && (ht(e, 2), Ef(e));
	}
	function Z(e, t, n) {
		if (e.tag === 3) pf(e, e, n);
		else for (; t !== null;) {
			if (t.tag === 3) {
				pf(t, e, n);
				break;
			}
			if (t.tag === 1) {
				var r = t.stateNode;
				if (typeof t.type.getDerivedStateFromError == "function" || typeof r.componentDidCatch == "function" && (yd === null || !yd.has(r))) {
					e = Wi(n, e), n = jc(2), r = yo(t, n, 2), r !== null && (Mc(n, r, t, e), ht(r, 2), Ef(r));
					break;
				}
			}
			t = t.return;
		}
	}
	function mf(e, t, n) {
		var r = e.pingCache;
		if (r === null) {
			r = e.pingCache = new ed();
			var i = /* @__PURE__ */ new Set();
			r.set(t, i);
		} else i = r.get(t), i === void 0 && (i = /* @__PURE__ */ new Set(), r.set(t, i));
		i.has(n) || (id = !0, i.add(n), e = hf.bind(null, e, t, n), t.then(e, e));
	}
	function hf(e, t, n) {
		var r = e.pingCache;
		r !== null && r.delete(t), e.pingedLanes |= e.suspendedLanes & n, e.warmLanes &= ~n, K === e && (J & n) === n && (od === 4 || od === 3 && (J & 62914560) === J && 300 > Ue() - hd ? G & 2 ? ld |= n : Vd(e, 0) : ld |= n, dd === J && (dd = 0)), Ef(e);
	}
	function gf(e, t) {
		t === 0 && (t = pt()), e = ki(e, t), e !== null && (ht(e, t), Ef(e));
	}
	function _f(e) {
		var t = e.memoizedState, n = 0;
		t !== null && (n = t.retryLane), gf(e, n);
	}
	function vf(e, t) {
		var n = 0;
		switch (e.tag) {
			case 31:
			case 13:
				var r = e.stateNode, a = e.memoizedState;
				a !== null && (n = a.retryLane);
				break;
			case 19:
				r = e.stateNode;
				break;
			case 22:
				r = e.stateNode._retryCache;
				break;
			default: throw Error(i(314));
		}
		r !== null && r.delete(t), gf(e, n);
	}
	function yf(e, t) {
		return ze(e, t);
	}
	var bf = null, xf = null, Sf = !1, Cf = !1, wf = !1, Tf = 0;
	function Ef(e) {
		e !== xf && e.next === null && (xf === null ? bf = xf = e : xf = xf.next = e), Cf = !0, Sf || (Sf = !0, Nf());
	}
	function Df(e, t) {
		if (!wf && Cf) {
			wf = !0;
			do
				for (var n = !1, r = bf; r !== null;) {
					if (!t) {
						if (e !== 0) {
							var i = r.pendingLanes;
							if (i === 0) var a = 0;
							else {
								var o = r.suspendedLanes, s = r.pingedLanes;
								a = (1 << 31 - tt(42 | e) + 1) - 1, a &= i & ~(o & ~s), a = a & 201326741 ? a & 201326741 | 1 : a ? a | 2 : 0;
							}
							a !== 0 && (n = !0, Mf(r, a));
						} else a = J, a = lt(r, r === K ? a : 0, r.cancelPendingCommit !== null || r.timeoutHandle !== -1), !(a & 3) || ut(r, a) || (n = !0, Mf(r, a));
					}
					r = r.next;
				}
			while (n);
			wf = !1;
		}
	}
	function Of() {
		kf();
	}
	function kf() {
		Cf = Sf = !1;
		var e = 0;
		Tf !== 0 && hp() && (e = Tf);
		for (var t = Ue(), n = null, r = bf; r !== null;) {
			var i = r.next, a = Af(r, t);
			a === 0 ? (r.next = null, n === null ? bf = i : n.next = i, i === null && (xf = n)) : (n = r, (e !== 0 || a & 3) && (Cf = !0)), r = i;
		}
		X !== 0 && X !== 5 || Df(e, !1), Tf !== 0 && (Tf = 0);
	}
	function Af(e, t) {
		for (var n = e.suspendedLanes, r = e.pingedLanes, i = e.expirationTimes, a = e.pendingLanes & -62914561; 0 < a;) {
			var o = 31 - tt(a), s = 1 << o, c = i[o];
			c === -1 ? ((s & n) === 0 || (s & r) !== 0) && (i[o] = ft(s, t)) : c <= t && (e.expiredLanes |= s), a &= ~s;
		}
		if (t = K, n = J, n = lt(e, e === t ? n : 0, e.cancelPendingCommit !== null || e.timeoutHandle !== -1), r = e.callbackNode, n === 0 || e === t && (Y === 2 || Y === 9) || e.cancelPendingCommit !== null) return r !== null && r !== null && Be(r), e.callbackNode = null, e.callbackPriority = 0;
		if (!(n & 3) || ut(e, n)) {
			if (t = n & -n, t === e.callbackPriority) return t;
			switch (r !== null && Be(r), xt(n)) {
				case 2:
				case 8:
					n = Ke;
					break;
				case 32:
					n = qe;
					break;
				case 268435456:
					n = Ye;
					break;
				default: n = qe;
			}
			return r = jf.bind(null, e), n = ze(n, r), e.callbackPriority = t, e.callbackNode = n, t;
		}
		return r !== null && r !== null && Be(r), e.callbackPriority = 2, e.callbackNode = null, 2;
	}
	function jf(e, t) {
		if (X !== 0 && X !== 5) return e.callbackNode = null, e.callbackPriority = 0, null;
		var n = e.callbackNode;
		if (df() && e.callbackNode !== n) return null;
		var r = J;
		return r = lt(e, e === K ? r : 0, e.cancelPendingCommit !== null || e.timeoutHandle !== -1), r === 0 ? null : (Fd(e, r, t), Af(e, Ue()), e.callbackNode != null && e.callbackNode === n ? jf.bind(null, e) : null);
	}
	function Mf(e, t) {
		if (df()) return null;
		Fd(e, t, !0);
	}
	function Nf() {
		bp(function() {
			G & 6 ? ze(Ge, Of) : kf();
		});
	}
	function Pf() {
		if (Tf === 0) {
			var e = Ba;
			e === 0 && (e = at, at <<= 1, !(at & 261888) && (at = 256)), Tf = e;
		}
		return Tf;
	}
	function Ff(e) {
		return e == null || typeof e == "symbol" || typeof e == "boolean" ? null : typeof e == "function" ? e : yn(e);
	}
	function If(e, t, n, r, i) {
		if (t === "submit" && n && n.stateNode === i) {
			var a = Ff((i[Tt] || null).action), o = r.submitter;
			o && (t = (t = o[Tt] || null) ? Ff(t.formAction) : o.getAttribute("formAction"), t !== null && (a = t, o = null));
			var s = new Vn("action", "action", null, r, i);
			e.push({
				event: s,
				listeners: [{
					instance: null,
					listener: function() {
						if (r.defaultPrevented) {
							if (Tf !== 0) {
								var e = new FormData(i, o);
								nc(n, {
									pending: !0,
									data: e,
									method: i.method,
									action: a
								}, null, e);
							}
						} else typeof a == "function" && (s.preventDefault(), e = new FormData(i, o), nc(n, {
							pending: !0,
							data: e,
							method: i.method,
							action: a
						}, a, e));
					},
					currentTarget: i
				}]
			});
		}
	}
	for (var Lf = 0; Lf < gi.length; Lf++) {
		var Rf = gi[Lf];
		_i(Rf.toLowerCase(), "on" + (Rf[0].toUpperCase() + Rf.slice(1)));
	}
	_i(ci, "onAnimationEnd"), _i(li, "onAnimationIteration"), _i(ui, "onAnimationStart"), _i("dblclick", "onDoubleClick"), _i("focusin", "onFocus"), _i("focusout", "onBlur"), _i(di, "onTransitionRun"), _i(fi, "onTransitionStart"), _i(pi, "onTransitionCancel"), _i(mi, "onTransitionEnd"), Ut("onMouseEnter", ["mouseout", "mouseover"]), Ut("onMouseLeave", ["mouseout", "mouseover"]), Ut("onPointerEnter", ["pointerout", "pointerover"]), Ut("onPointerLeave", ["pointerout", "pointerover"]), Ht("onChange", "change click focusin focusout input keydown keyup selectionchange".split(" ")), Ht("onSelect", "focusout contextmenu dragend focusin keydown keyup mousedown mouseup selectionchange".split(" ")), Ht("onBeforeInput", [
		"compositionend",
		"keypress",
		"textInput",
		"paste"
	]), Ht("onCompositionEnd", "compositionend focusout keydown keypress keyup mousedown".split(" ")), Ht("onCompositionStart", "compositionstart focusout keydown keypress keyup mousedown".split(" ")), Ht("onCompositionUpdate", "compositionupdate focusout keydown keypress keyup mousedown".split(" "));
	var zf = "abort canplay canplaythrough durationchange emptied encrypted ended error loadeddata loadedmetadata loadstart pause play playing progress ratechange resize seeked seeking stalled suspend timeupdate volumechange waiting".split(" "), Bf = new Set("beforetoggle cancel close invalid load scroll scrollend toggle".split(" ").concat(zf));
	function Vf(e, t) {
		t = !!(t & 4);
		for (var n = 0; n < e.length; n++) {
			var r = e[n], i = r.event;
			r = r.listeners;
			a: {
				var a = void 0;
				if (t) for (var o = r.length - 1; 0 <= o; o--) {
					var s = r[o], c = s.instance, l = s.currentTarget;
					if (s = s.listener, c !== a && i.isPropagationStopped()) break a;
					a = s, i.currentTarget = l;
					try {
						a(i);
					} catch (e) {
						Si(e);
					}
					i.currentTarget = null, a = c;
				}
				else for (o = 0; o < r.length; o++) {
					if (s = r[o], c = s.instance, l = s.currentTarget, s = s.listener, c !== a && i.isPropagationStopped()) break a;
					a = s, i.currentTarget = l;
					try {
						a(i);
					} catch (e) {
						Si(e);
					}
					i.currentTarget = null, a = c;
				}
			}
		}
	}
	function Q(e, t) {
		var n = t[Dt];
		n === void 0 && (n = t[Dt] = /* @__PURE__ */ new Set());
		var r = e + "__bubble";
		n.has(r) || (Gf(t, e, 2, !1), n.add(r));
	}
	function Hf(e, t, n) {
		var r = 0;
		t && (r |= 4), Gf(n, e, r, t);
	}
	var Uf = "_reactListening" + Math.random().toString(36).slice(2);
	function Wf(e) {
		if (!e[Uf]) {
			e[Uf] = !0, Bt.forEach(function(t) {
				t !== "selectionchange" && (Bf.has(t) || Hf(t, !1, e), Hf(t, !0, e));
			});
			var t = e.nodeType === 9 ? e : e.ownerDocument;
			t === null || t[Uf] || (t[Uf] = !0, Hf("selectionchange", !1, t));
		}
	}
	function Gf(e, t, n, r) {
		switch (Ch(t)) {
			case 2:
				var i = _h;
				break;
			case 8:
				i = vh;
				break;
			default: i = yh;
		}
		n = i.bind(null, t, n, e), i = void 0, !An || t !== "touchstart" && t !== "touchmove" && t !== "wheel" || (i = !0), r ? i === void 0 ? e.addEventListener(t, n, !0) : e.addEventListener(t, n, {
			capture: !0,
			passive: i
		}) : i === void 0 ? e.addEventListener(t, n, !1) : e.addEventListener(t, n, { passive: i });
	}
	function Kf(e, t, n, r, i) {
		var a = r;
		if (!(t & 1) && !(t & 2) && r !== null) a: for (;;) {
			if (r === null) return;
			var s = r.tag;
			if (s === 3 || s === 4) {
				var c = r.stateNode.containerInfo;
				if (c === i) break;
				if (s === 4) for (s = r.return; s !== null;) {
					var l = s.tag;
					if ((l === 3 || l === 4) && s.stateNode.containerInfo === i) return;
					s = s.return;
				}
				for (; c !== null;) {
					if (s = Pt(c), s === null) return;
					if (l = s.tag, l === 5 || l === 6 || l === 26 || l === 27) {
						r = a = s;
						continue a;
					}
					c = c.parentNode;
				}
			}
			r = r.return;
		}
		Dn(function() {
			var r = a, i = Sn(n), s = [];
			a: {
				var c = hi.get(e);
				if (c !== void 0) {
					var l = Vn, u = e;
					switch (e) {
						case "keypress": if (In(n) === 0) break a;
						case "keydown":
						case "keyup":
							l = ar;
							break;
						case "focusin":
							u = "focus", l = Xn;
							break;
						case "focusout":
							u = "blur", l = Xn;
							break;
						case "beforeblur":
						case "afterblur":
							l = Xn;
							break;
						case "click": if (n.button === 2) break a;
						case "auxclick":
						case "dblclick":
						case "mousedown":
						case "mousemove":
						case "mouseup":
						case "mouseout":
						case "mouseover":
						case "contextmenu":
							l = Jn;
							break;
						case "drag":
						case "dragend":
						case "dragenter":
						case "dragexit":
						case "dragleave":
						case "dragover":
						case "dragstart":
						case "drop":
							l = Yn;
							break;
						case "touchcancel":
						case "touchend":
						case "touchmove":
						case "touchstart":
							l = cr;
							break;
						case ci:
						case li:
						case ui:
							l = Zn;
							break;
						case mi:
							l = lr;
							break;
						case "scroll":
						case "scrollend":
							l = Un;
							break;
						case "wheel":
							l = ur;
							break;
						case "copy":
						case "cut":
						case "paste":
							l = Qn;
							break;
						case "gotpointercapture":
						case "lostpointercapture":
						case "pointercancel":
						case "pointerdown":
						case "pointermove":
						case "pointerout":
						case "pointerover":
						case "pointerup":
							l = or;
							break;
						case "submit":
							l = sr;
							break;
						case "toggle":
						case "beforetoggle": l = dr;
					}
					var d = !!(t & 4), f = !d && (e === "scroll" || e === "scrollend"), p = d ? c === null ? null : c + "Capture" : c;
					d = [];
					for (var m = r, h; m !== null;) {
						var g = m;
						if (h = g.stateNode, g = g.tag, g !== 5 && g !== 26 && g !== 27 || h === null || p === null || (g = On(m, p), g != null && d.push(qf(m, g, h))), f) break;
						m = m.return;
					}
					0 < d.length && (c = new l(c, u, null, n, i), s.push({
						event: c,
						listeners: d
					}));
				}
			}
			if (!(t & 7)) {
				a: {
					if (l = e === "mouseover" || e === "pointerover", c = e === "mouseout" || e === "pointerout", l && n !== xn && (u = n.relatedTarget || n.fromElement) && (Pt(u) || u[Et])) break a;
					(c || l) && (u = i.window === i ? i : (l = i.ownerDocument) ? l.defaultView || l.parentWindow : window, c ? (l = n.relatedTarget || n.toElement, c = r, l = l ? Pt(l) : null, l !== null && (f = o(l), d = l.tag, l !== f || d !== 5 && d !== 27 && d !== 6) && (l = null)) : (c = null, l = r), c !== l && (d = Jn, g = "onMouseLeave", p = "onMouseEnter", m = "mouse", (e === "pointerout" || e === "pointerover") && (d = or, g = "onPointerLeave", p = "onPointerEnter", m = "pointer"), f = c == null ? u : It(c), h = l == null ? u : It(l), u = new d(g, m + "leave", c, n, i), u.target = f, u.relatedTarget = h, g = null, Pt(i) === r && (d = new d(p, m + "enter", l, n, i), d.target = h, d.relatedTarget = f, g = d), f = g, d = c && l ? E(c, l, Yf) : null, c !== null && Xf(s, u, c, d, !1), l !== null && f !== null && Xf(s, f, l, d, !0)));
				}
				a: {
					if (c = r ? It(r) : window, l = c.nodeName && c.nodeName.toLowerCase(), l === "select" || l === "input" && c.type === "file") var _ = jr;
					else if (Tr(c)) {
						if (Mr) _ = Vr;
						else {
							_ = zr;
							var v = Rr;
						}
					} else l = c.nodeName, !l || l.toLowerCase() !== "input" || c.type !== "checkbox" && c.type !== "radio" ? r && gn(r.elementType) && (_ = jr) : _ = Br;
					if (_ &&= _(e, r)) {
						Er(s, _, n, i);
						break a;
					}
					v && v(e, c, r);
				}
				switch (v = r ? It(r) : window, e) {
					case "focusin":
						(Tr(v) || v.contentEditable === "true") && (Qr = v, $r = r, ei = null);
						break;
					case "focusout":
						ei = $r = Qr = null;
						break;
					case "mousedown":
						ti = !0;
						break;
					case "contextmenu":
					case "mouseup":
					case "dragend":
						ti = !1, ni(s, n, i);
						break;
					case "selectionchange": if (Zr) break;
					case "keydown":
					case "keyup": ni(s, n, i);
				}
				var y;
				if (pr) b: {
					switch (e) {
						case "compositionstart":
							var b = "onCompositionStart";
							break b;
						case "compositionend":
							b = "onCompositionEnd";
							break b;
						case "compositionupdate":
							b = "onCompositionUpdate";
							break b;
					}
					b = void 0;
				}
				else xr ? yr(e, n) && (b = "onCompositionEnd") : e === "keydown" && n.keyCode === 229 && (b = "onCompositionStart");
				b && (gr && n.locale !== "ko" && (xr || b !== "onCompositionStart" ? b === "onCompositionEnd" && xr && (y = Fn()) : (Mn = i, Nn = "value" in Mn ? Mn.value : Mn.textContent, xr = !0)), v = Jf(r, b), 0 < v.length && (b = new $n(b, e, null, n, i), s.push({
					event: b,
					listeners: v
				}), y ? b.data = y : (y = br(n), y !== null && (b.data = y)))), (y = hr ? Sr(e, n) : Cr(e, n)) && (b = Jf(r, "onBeforeInput"), 0 < b.length && (v = new $n("onBeforeInput", "beforeinput", null, n, i), s.push({
					event: v,
					listeners: b
				}), v.data = y)), If(s, e, r, n, i);
			}
			Vf(s, t);
		});
	}
	function qf(e, t, n) {
		return {
			instance: e,
			listener: t,
			currentTarget: n
		};
	}
	function Jf(e, t) {
		for (var n = t + "Capture", r = []; e !== null;) {
			var i = e, a = i.stateNode;
			if (i = i.tag, i !== 5 && i !== 26 && i !== 27 || a === null || (i = On(e, n), i != null && r.unshift(qf(e, i, a)), i = On(e, t), i != null && r.push(qf(e, i, a))), e.tag === 3) return r;
			e = e.return;
		}
		return [];
	}
	function Yf(e) {
		if (e === null) return null;
		do
			e = e.return;
		while (e && e.tag !== 5 && e.tag !== 27);
		return e || null;
	}
	function Xf(e, t, n, r, i) {
		for (var a = t._reactName, o = []; n !== null && n !== r;) {
			var s = n, c = s.alternate, l = s.stateNode;
			if (s = s.tag, c !== null && c === r) break;
			s !== 5 && s !== 26 && s !== 27 || l === null || (c = l, i ? (l = On(n, a), l != null && o.unshift(qf(n, l, c))) : i || (l = On(n, a), l != null && o.push(qf(n, l, c)))), n = n.return;
		}
		o.length !== 0 && e.push({
			event: t,
			listeners: o
		});
	}
	var Zf = /\r\n?/g, Qf = /\u0000|\uFFFD/g;
	function $f(e) {
		return (typeof e == "string" ? e : "" + e).replace(Zf, "\n").replace(Qf, "");
	}
	function ep(e, t) {
		return t = $f(t), $f(e) === t;
	}
	function $(e, t, n, r, a, o) {
		switch (n) {
			case "children":
				if (typeof r == "string") t === "body" || t === "textarea" && r === "" || fn(e, r);
				else if (typeof r == "number" || typeof r == "bigint") t !== "body" && fn(e, "" + r);
				else return;
				break;
			case "className":
				Xt(e, "class", r);
				break;
			case "tabIndex":
				Xt(e, "tabindex", r);
				break;
			case "dir":
			case "role":
			case "viewBox":
			case "width":
			case "height":
				Xt(e, n, r);
				break;
			case "style":
				hn(e, r, o);
				return;
			case "data": if (t !== "object") {
				Xt(e, "data", r);
				break;
			}
			case "src":
			case "href":
				if (r === "" && (t !== "a" || n !== "href")) {
					e.removeAttribute(n);
					break;
				}
				if (r == null || typeof r == "function" || typeof r == "symbol" || typeof r == "boolean") {
					e.removeAttribute(n);
					break;
				}
				r = yn(r), e.setAttribute(n, r);
				break;
			case "action":
			case "formAction":
				if (typeof r == "function") {
					e.setAttribute(n, "javascript:throw new Error('A React form was unexpectedly submitted. If you called form.submit() manually, consider using form.requestSubmit() instead. If you\\'re trying to use event.stopPropagation() in a submit event handler, consider also calling event.preventDefault().')");
					break;
				}
				if (typeof o == "function" && (n === "formAction" ? (t !== "input" && $(e, t, "name", a.name, a, null), $(e, t, "formEncType", a.formEncType, a, null), $(e, t, "formMethod", a.formMethod, a, null), $(e, t, "formTarget", a.formTarget, a, null)) : ($(e, t, "encType", a.encType, a, null), $(e, t, "method", a.method, a, null), $(e, t, "target", a.target, a, null))), r == null || typeof r == "symbol" || typeof r == "boolean") {
					e.removeAttribute(n);
					break;
				}
				r = yn(r), e.setAttribute(n, r);
				break;
			case "onClick":
				r != null && (e.onclick = bn);
				return;
			case "onScroll":
				r != null && Q("scroll", e);
				return;
			case "onScrollEnd":
				r != null && Q("scrollend", e);
				return;
			case "dangerouslySetInnerHTML":
				if (r != null) {
					if (typeof r != "object" || !("__html" in r)) throw Error(i(61));
					if (n = r.__html, n != null) {
						if (a.children != null) throw Error(i(60));
						o?.__html !== n && (e.innerHTML = n);
					}
				}
				break;
			case "multiple":
				e.multiple = r && typeof r != "function" && typeof r != "symbol";
				break;
			case "muted":
				e.muted = r && typeof r != "function" && typeof r != "symbol";
				break;
			case "suppressContentEditableWarning":
			case "suppressHydrationWarning":
			case "defaultValue":
			case "defaultChecked":
			case "innerHTML":
			case "ref": break;
			case "autoFocus": break;
			case "xlinkHref":
				if (r == null || typeof r == "function" || typeof r == "boolean" || typeof r == "symbol") {
					e.removeAttribute("xlink:href");
					break;
				}
				n = yn(r), e.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", n);
				break;
			case "contentEditable":
			case "spellCheck":
			case "draggable":
			case "value":
			case "autoReverse":
			case "externalResourcesRequired":
			case "focusable":
			case "preserveAlpha":
				r != null && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, r) : e.removeAttribute(n);
				break;
			case "inert":
			case "allowFullScreen":
			case "async":
			case "autoPlay":
			case "controls":
			case "credentialless":
			case "default":
			case "defer":
			case "disabled":
			case "disablePictureInPicture":
			case "disableRemotePlayback":
			case "formNoValidate":
			case "hidden":
			case "loop":
			case "noModule":
			case "noValidate":
			case "open":
			case "playsInline":
			case "readOnly":
			case "required":
			case "reversed":
			case "scoped":
			case "seamless":
			case "itemScope":
				r && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, "") : e.removeAttribute(n);
				break;
			case "capture":
			case "download":
				!0 === r ? e.setAttribute(n, "") : !1 !== r && r != null && typeof r != "function" && typeof r != "symbol" ? e.setAttribute(n, r) : e.removeAttribute(n);
				break;
			case "cols":
			case "rows":
			case "size":
			case "span":
				r != null && typeof r != "function" && typeof r != "symbol" && !isNaN(r) && 1 <= r ? e.setAttribute(n, r) : e.removeAttribute(n);
				break;
			case "rowSpan":
			case "start":
				r == null || typeof r == "function" || typeof r == "symbol" || isNaN(r) ? e.removeAttribute(n) : e.setAttribute(n, r);
				break;
			case "popover":
				Q("beforetoggle", e), Q("toggle", e), Yt(e, "popover", r);
				break;
			case "xlinkActuate":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:actuate", r);
				break;
			case "xlinkArcrole":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:arcrole", r);
				break;
			case "xlinkRole":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:role", r);
				break;
			case "xlinkShow":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:show", r);
				break;
			case "xlinkTitle":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:title", r);
				break;
			case "xlinkType":
				Zt(e, "http://www.w3.org/1999/xlink", "xlink:type", r);
				break;
			case "xmlBase":
				Zt(e, "http://www.w3.org/XML/1998/namespace", "xml:base", r);
				break;
			case "xmlLang":
				Zt(e, "http://www.w3.org/XML/1998/namespace", "xml:lang", r);
				break;
			case "xmlSpace":
				Zt(e, "http://www.w3.org/XML/1998/namespace", "xml:space", r);
				break;
			case "is":
				Yt(e, "is", r);
				break;
			case "innerText":
			case "textContent": return;
			default: if (!(2 < n.length) || n[0] !== "o" && n[0] !== "O" || n[1] !== "n" && n[1] !== "N") n = _n.get(n) || n, Yt(e, n, r);
			else return;
		}
		L = !0;
	}
	function tp(e, t, n, r, a, o) {
		switch (n) {
			case "style":
				hn(e, r, o);
				return;
			case "dangerouslySetInnerHTML":
				if (r != null) {
					if (typeof r != "object" || !("__html" in r)) throw Error(i(61));
					if (n = r.__html, n != null) {
						if (a.children != null) throw Error(i(60));
						o?.__html !== n && (e.innerHTML = n);
					}
				}
				break;
			case "children":
				if (typeof r == "string") fn(e, r);
				else if (typeof r == "number" || typeof r == "bigint") fn(e, "" + r);
				else return;
				break;
			case "onScroll":
				r != null && Q("scroll", e);
				return;
			case "onScrollEnd":
				r != null && Q("scrollend", e);
				return;
			case "onClick":
				r != null && (e.onclick = bn);
				return;
			case "suppressContentEditableWarning":
			case "suppressHydrationWarning":
			case "innerHTML":
			case "ref": return;
			case "innerText":
			case "textContent": return;
			default:
				if (!Vt.hasOwnProperty(n)) a: {
					if (n[0] === "o" && n[1] === "n" && (a = n.endsWith("Capture"), o = n.slice(2, a ? n.length - 7 : void 0), t = e[Tt] || null, t = t == null ? null : t[n], typeof t == "function" && e.removeEventListener(o, t, a), typeof r == "function")) {
						typeof t != "function" && t !== null && (n in e ? e[n] = null : e.hasAttribute(n) && e.removeAttribute(n)), e.addEventListener(o, r, a);
						break a;
					}
					L = !0, n in e ? e[n] = r : !0 === r ? e.setAttribute(n, "") : Yt(e, n, r);
				}
				return;
		}
		L = !0;
	}
	function np(e, t, n) {
		switch (t) {
			case "div":
			case "span":
			case "svg":
			case "path":
			case "a":
			case "g":
			case "p":
			case "li": break;
			case "img":
				Q("error", e), Q("load", e);
				var r = !1, a = !1, o;
				for (o in n) if (n.hasOwnProperty(o)) {
					var s = n[o];
					if (s != null) switch (o) {
						case "src":
							r = !0;
							break;
						case "srcSet":
							a = !0;
							break;
						case "children":
						case "dangerouslySetInnerHTML": throw Error(i(137, t));
						default: $(e, t, o, s, n, null);
					}
				}
				a && $(e, t, "srcSet", n.srcSet, n, null), r && $(e, t, "src", n.src, n, null);
				return;
			case "input":
				Q("invalid", e);
				var c = o = s = a = null, l = null, u = null;
				for (r in n) if (n.hasOwnProperty(r)) {
					var d = n[r];
					if (d != null) switch (r) {
						case "name":
							a = d;
							break;
						case "type":
							s = d;
							break;
						case "checked":
							l = d;
							break;
						case "defaultChecked":
							u = d;
							break;
						case "value":
							o = d;
							break;
						case "defaultValue":
							c = d;
							break;
						case "children":
						case "dangerouslySetInnerHTML":
							if (d != null) throw Error(i(137, t));
							break;
						default: $(e, t, r, d, n, null);
					}
				}
				sn(e, o, c, l, u, s, a, !1);
				return;
			case "select":
				for (a in Q("invalid", e), r = s = o = null, n) if (n.hasOwnProperty(a) && (c = n[a], c != null)) switch (a) {
					case "value":
						o = c;
						break;
					case "defaultValue":
						s = c;
						break;
					case "multiple": r = c;
					default: $(e, t, a, c, n, null);
				}
				t = o, n = s, e.multiple = !!r, t == null ? n != null && ln(e, !!r, n, !0) : ln(e, !!r, t, !1);
				return;
			case "textarea":
				for (s in Q("invalid", e), o = a = r = null, n) if (n.hasOwnProperty(s) && (c = n[s], c != null)) switch (s) {
					case "value":
						r = c;
						break;
					case "defaultValue":
						a = c;
						break;
					case "children":
						o = c;
						break;
					case "dangerouslySetInnerHTML":
						if (c != null) throw Error(i(91));
						break;
					default: $(e, t, s, c, n, null);
				}
				dn(e, r, a, o);
				return;
			case "option":
				for (l in n) if (n.hasOwnProperty(l) && (r = n[l], r != null)) switch (l) {
					case "selected":
						e.selected = r && typeof r != "function" && typeof r != "symbol";
						break;
					default: $(e, t, l, r, n, null);
				}
				return;
			case "dialog":
				Q("beforetoggle", e), Q("toggle", e), Q("cancel", e), Q("close", e);
				break;
			case "iframe":
			case "object":
				Q("load", e);
				break;
			case "video":
			case "audio":
				for (r = 0; r < zf.length; r++) Q(zf[r], e);
				break;
			case "image":
				Q("error", e), Q("load", e);
				break;
			case "details":
				Q("toggle", e);
				break;
			case "embed":
			case "source":
			case "link": Q("error", e), Q("load", e);
			case "area":
			case "base":
			case "br":
			case "col":
			case "hr":
			case "keygen":
			case "meta":
			case "param":
			case "track":
			case "wbr":
			case "menuitem":
				for (u in n) if (n.hasOwnProperty(u) && (r = n[u], r != null)) switch (u) {
					case "children":
					case "dangerouslySetInnerHTML": throw Error(i(137, t));
					default: $(e, t, u, r, n, null);
				}
				return;
			default: if (gn(t)) {
				for (d in n) n.hasOwnProperty(d) && (r = n[d], r !== void 0 && tp(e, t, d, r, n, void 0));
				return;
			}
		}
		for (c in n) n.hasOwnProperty(c) && (r = n[c], r != null && $(e, t, c, r, n, null));
	}
	var rp = {};
	function ip(e, t, n, r) {
		switch (t) {
			case "div":
			case "span":
			case "svg":
			case "path":
			case "a":
			case "g":
			case "p":
			case "li": break;
			case "input":
				var a = null, o = null, s = null, c = null, l = null, u = null, d = null;
				for (m in n) {
					var f = n[m];
					if (n.hasOwnProperty(m) && f != null) switch (m) {
						case "checked": break;
						case "value": break;
						case "defaultValue": l = f;
						default: r.hasOwnProperty(m) || $(e, t, m, null, r, f);
					}
				}
				for (var p in r) {
					var m = r[p];
					if (f = n[p], r.hasOwnProperty(p) && (m != null || f != null)) switch (p) {
						case "type":
							m !== f && (L = !0), o = m;
							break;
						case "name":
							m !== f && (L = !0), a = m;
							break;
						case "checked":
							m !== f && (L = !0), u = m;
							break;
						case "defaultChecked":
							m !== f && (L = !0), d = m;
							break;
						case "value":
							m !== f && (L = !0), s = m;
							break;
						case "defaultValue":
							m !== f && (L = !0), c = m;
							break;
						case "children":
						case "dangerouslySetInnerHTML":
							if (m != null) throw Error(i(137, t));
							break;
						default: m !== f && $(e, t, p, m, r, f);
					}
				}
				on(e, s, c, l, u, d, o, a);
				return;
			case "select":
				for (o in m = s = c = p = null, n) if (l = n[o], n.hasOwnProperty(o) && l != null) switch (o) {
					case "value": break;
					case "multiple": m = l;
					default: r.hasOwnProperty(o) || $(e, t, o, null, r, l);
				}
				for (a in r) if (o = r[a], l = n[a], r.hasOwnProperty(a) && (o != null || l != null)) switch (a) {
					case "value":
						o !== l && (L = !0), p = o;
						break;
					case "defaultValue":
						o !== l && (L = !0), c = o;
						break;
					case "multiple": o !== l && (L = !0), s = o;
					default: o !== l && $(e, t, a, o, r, l);
				}
				t = c, n = s, r = m, p == null ? !!r != !!n && (t == null ? ln(e, !!n, n ? [] : "", !1) : ln(e, !!n, t, !0)) : ln(e, !!n, p, !1);
				return;
			case "textarea":
				for (c in m = p = null, n) if (a = n[c], n.hasOwnProperty(c) && a != null && !r.hasOwnProperty(c)) switch (c) {
					case "value": break;
					case "children": break;
					default: $(e, t, c, null, r, a);
				}
				for (s in r) if (a = r[s], o = n[s], r.hasOwnProperty(s) && (a != null || o != null)) switch (s) {
					case "value":
						a !== o && (L = !0), p = a;
						break;
					case "defaultValue":
						a !== o && (L = !0), m = a;
						break;
					case "children": break;
					case "dangerouslySetInnerHTML":
						if (a != null) throw Error(i(91));
						break;
					default: a !== o && $(e, t, s, a, r, o);
				}
				un(e, p, m);
				return;
			case "option":
				for (var h in n) if (p = n[h], n.hasOwnProperty(h) && p != null && !r.hasOwnProperty(h)) switch (h) {
					case "selected":
						e.selected = !1;
						break;
					default: $(e, t, h, null, r, p);
				}
				for (l in r) if (p = r[l], m = n[l], r.hasOwnProperty(l) && p !== m && (p != null || m != null)) switch (l) {
					case "selected":
						p !== m && (L = !0), e.selected = p && typeof p != "function" && typeof p != "symbol";
						break;
					default: $(e, t, l, p, r, m);
				}
				return;
			case "img":
			case "link":
			case "area":
			case "base":
			case "br":
			case "col":
			case "embed":
			case "hr":
			case "keygen":
			case "meta":
			case "param":
			case "source":
			case "track":
			case "wbr":
			case "menuitem":
				for (var g in n) p = n[g], n.hasOwnProperty(g) && p != null && !r.hasOwnProperty(g) && $(e, t, g, null, r, p);
				for (u in r) if (p = r[u], m = n[u], r.hasOwnProperty(u) && p !== m && (p != null || m != null)) switch (u) {
					case "children":
					case "dangerouslySetInnerHTML":
						if (p != null) throw Error(i(137, t));
						break;
					default: $(e, t, u, p, r, m);
				}
				return;
			default: if (gn(t)) {
				for (var _ in n) p = n[_], n.hasOwnProperty(_) && p !== void 0 && !r.hasOwnProperty(_) && tp(e, t, _, void 0, r, p);
				for (d in r) p = r[d], m = n[d], !r.hasOwnProperty(d) || p === m || p === void 0 && m === void 0 || tp(e, t, d, p, r, m);
				return;
			}
		}
		for (var v in n) p = n[v], n.hasOwnProperty(v) && p != null && !r.hasOwnProperty(v) && $(e, t, v, null, r, p);
		for (f in r) p = r[f], m = n[f], !r.hasOwnProperty(f) || p === m || p == null && m == null || $(e, t, f, p, r, m);
	}
	function ap(e) {
		switch (e) {
			case "css":
			case "script":
			case "font":
			case "img":
			case "image":
			case "input":
			case "link": return !0;
			default: return !1;
		}
	}
	function op() {
		if (typeof performance.getEntriesByType == "function") {
			for (var e = 0, t = 0, n = performance.getEntriesByType("resource"), r = 0; r < n.length; r++) {
				var i = n[r], a = i.transferSize, o = i.initiatorType, s = i.duration;
				if (a && s && ap(o)) {
					for (o = 0, s = i.responseEnd, r += 1; r < n.length; r++) {
						var c = n[r], l = c.startTime;
						if (l > s) break;
						var u = c.transferSize, d = c.initiatorType;
						u && ap(d) && (c = c.responseEnd, o += u * (c < s ? 1 : (s - l) / (c - l)));
					}
					if (--r, t += 8 * (a + o) / (i.duration / 1e3), e++, 10 < e) break;
				}
			}
			if (0 < e) return t / e / 1e6;
		}
		return navigator.connection && (e = navigator.connection.downlink, typeof e == "number") ? e : 5;
	}
	var sp = null, cp = null;
	function lp(e) {
		return e.nodeType === 9 ? e : e.ownerDocument;
	}
	function up(e) {
		switch (e) {
			case "http://www.w3.org/2000/svg": return 1;
			case "http://www.w3.org/1998/Math/MathML": return 2;
			default: return 0;
		}
	}
	function dp(e, t) {
		if (e === 0) switch (t) {
			case "svg": return 1;
			case "math": return 2;
			default: return 0;
		}
		return e === 1 && t === "foreignObject" ? 0 : e;
	}
	function fp(e, t, n, r) {
		return n = lp(n).createElement(e), n[I] = r, n[Tt] = t, np(n, e, t), Rt(n), n;
	}
	function pp(e, t) {
		return e === "textarea" || e === "noscript" || typeof t.children == "string" || typeof t.children == "number" || typeof t.children == "bigint" || typeof t.dangerouslySetInnerHTML == "object" && t.dangerouslySetInnerHTML !== null && t.dangerouslySetInnerHTML.__html != null;
	}
	var mp = null;
	function hp() {
		var e = window.event;
		return e && e.type === "popstate" ? e !== mp && (mp = e, !0) : (mp = null, !1);
	}
	var gp = typeof setTimeout == "function" ? setTimeout : void 0, _p = typeof clearTimeout == "function" ? clearTimeout : void 0, vp = typeof Promise == "function" ? Promise : void 0, yp = typeof requestAnimationFrame == "function" ? requestAnimationFrame : gp, bp = typeof queueMicrotask == "function" ? queueMicrotask : vp === void 0 ? gp : function(e) {
		return vp.resolve(null).then(e).catch(xp);
	};
	function xp(e) {
		setTimeout(function() {
			throw e;
		});
	}
	function Sp(e) {
		return e === "head";
	}
	function Cp(e, t) {
		var n = t, r = 0;
		do {
			var i = n.nextSibling;
			if (e.removeChild(n), i && i.nodeType === 8) {
				if (n = i.data, n === "/$" || n === "/&") {
					if (r === 0) {
						e.removeChild(i), Hh(t);
						return;
					}
					r--;
				} else if (n === "$" || n === "$?" || n === "$~" || n === "$!" || n === "&") r++;
				else if (n === "html") _m(e.ownerDocument.documentElement);
				else if (n === "head") {
					n = e.ownerDocument.head, _m(n);
					for (var a = n.firstChild; a;) {
						var o = a.nextSibling, s = a.nodeName;
						a[jt] || s === "SCRIPT" || s === "STYLE" || s === "LINK" && a.rel.toLowerCase() === "stylesheet" || n.removeChild(a), a = o;
					}
				} else n === "body" && _m(e.ownerDocument.body);
			}
			n = i;
		} while (n);
		Hh(t);
	}
	function wp(e, t) {
		var n = e;
		e = 0;
		do {
			var r = n.nextSibling;
			if (n.nodeType === 1 ? t ? (n._stashedDisplay = n.style.display, n.style.display = "none") : (n.style.display = n._stashedDisplay || "", n.getAttribute("style") === "" && n.removeAttribute("style")) : n.nodeType === 3 && (t ? (n._stashedText = n.nodeValue, n.nodeValue = "") : n.nodeValue = n._stashedText || ""), r && r.nodeType === 8) {
				if (n = r.data, n === "/$") {
					if (e === 0) break;
					e--;
				} else n !== "$" && n !== "$?" && n !== "$~" && n !== "$!" || e++;
			}
			n = r;
		} while (n);
	}
	function Tp(e, t, n) {
		if (t = CSS.escape(t) === t ? t : "r-" + btoa(t).replace(/=/g, ""), e.style.viewTransitionName = t, n != null && (e.style.viewTransitionClass = n), n = getComputedStyle(e), n.display === "inline") {
			if (t = e.getClientRects(), t.length === 1) var r = 1;
			else for (var i = r = 0; i < t.length; i++) {
				var a = t[i];
				0 < a.width && 0 < a.height && r++;
			}
			r === 1 && (e = e.style, e.display = t.length === 1 ? "inline-block" : "block", e.marginTop = "-" + n.paddingTop, e.marginBottom = "-" + n.paddingBottom);
		}
	}
	function Ep(e, t) {
		e = e.style, t = t.style;
		var n = t == null ? null : t.hasOwnProperty("viewTransitionName") ? t.viewTransitionName : t.hasOwnProperty("view-transition-name") ? t["view-transition-name"] : null;
		e.viewTransitionName = n == null || typeof n == "boolean" ? "" : ("" + n).trim(), n = t == null ? null : t.hasOwnProperty("viewTransitionClass") ? t.viewTransitionClass : t.hasOwnProperty("view-transition-class") ? t["view-transition-class"] : null, e.viewTransitionClass = n == null || typeof n == "boolean" ? "" : ("" + n).trim(), e.display === "inline-block" && (t == null ? e.display = e.margin = "" : (n = t.display, e.display = n == null || typeof n == "boolean" ? "" : n, n = t.margin, n == null ? (n = t.hasOwnProperty("marginTop") ? t.marginTop : t["margin-top"], e.marginTop = n == null || typeof n == "boolean" ? "" : n, t = t.hasOwnProperty("marginBottom") ? t.marginBottom : t["margin-bottom"], e.marginBottom = t == null || typeof t == "boolean" ? "" : t) : e.margin = n));
	}
	function Dp(e, t, n) {
		return n = n.ownerDocument.defaultView, {
			rect: e,
			abs: t.position === "absolute" || t.position === "fixed",
			clip: t.clipPath !== "none" || t.overflow !== "visible" || t.filter !== "none" || t.mask !== "none" || t.mask !== "none" || t.borderRadius !== "0px",
			view: 0 <= e.bottom && 0 <= e.right && e.top <= n.innerHeight && e.left <= n.innerWidth
		};
	}
	function Op(e) {
		return Dp(e.getBoundingClientRect(), getComputedStyle(e), e);
	}
	function kp(e) {
		var t = e.getBoundingClientRect();
		t = new DOMRect(t.x + 2e4, t.y + 2e4, t.width, t.height);
		var n = getComputedStyle(e);
		return Dp(t, n, e);
	}
	function Ap(e) {
		return e.documentElement.clientHeight;
	}
	function jp(e) {
		this.addEventListener("load", e), this.addEventListener("error", e);
	}
	function Mp(e, t, n, r, i, a, o, s, c) {
		var l = t.nodeType === 9 ? t : t.ownerDocument;
		try {
			var u = l.startViewTransition({
				update: function() {
					var t = l.defaultView, n = t.navigation && t.navigation.transition, o = l.fonts.status;
					r();
					var s = [];
					if (o === "loaded" && (Ap(l), l.fonts.status === "loading" && s.push(l.fonts.ready)), o = s.length, e !== null) for (var c = e.suspenseyImages, u = 0, d = 0; d < c.length; d++) {
						var f = c[d];
						if (!f.complete) {
							var p = f.getBoundingClientRect();
							if (0 < p.bottom && 0 < p.right && p.top < t.innerHeight && p.left < t.innerWidth) {
								if (u += Xm(f), u > $m) {
									s.length = o;
									break;
								}
								f = new Promise(jp.bind(f)), s.push(f);
							}
						}
					}
					if (0 < s.length) return t = Promise.race([Promise.all(s), new Promise(function(e) {
						return setTimeout(e, 500);
					})]).then(i, i), (n ? Promise.allSettled([n.finished, t]) : t).then(a, a);
					if (i(), n) return n.finished.then(a, a);
					a();
				},
				types: n
			});
			l.__reactViewTransition = u;
			var d = [];
			return u.ready.then(function() {
				for (var e = l.documentElement.getAnimations({ subtree: !0 }), t = 0; t < e.length; t++) {
					var n = e[t], r = n.effect, i = r.pseudoElement;
					if (i != null && i.startsWith("::view-transition")) {
						d.push(n), n = r.getKeyframes();
						for (var a = i = void 0, s = !0, c = 0; c < n.length; c++) {
							var u = n[c], f = u.width;
							if (i === void 0) i = f;
							else if (i !== f) {
								s = !1;
								break;
							}
							if (f = u.height, a === void 0) a = f;
							else if (a !== f) {
								s = !1;
								break;
							}
							delete u.width, delete u.height, u.transform === "none" && delete u.transform;
						}
						s && i !== void 0 && a !== void 0 && (r.setKeyframes(n), s = getComputedStyle(r.target, r.pseudoElement), s.width !== i || s.height !== a) && (s = n[0], s.width = i, s.height = a, s = n[n.length - 1], s.width = i, s.height = a, r.setKeyframes(n));
					}
				}
				o();
			}, function(e) {
				l.__reactViewTransition === u && (l.__reactViewTransition = null);
				try {
					if (typeof e == "object" && e) switch (e.name) {
						case "InvalidStateError": (e.message === "View transition was skipped because document visibility state is hidden." || e.message === "Skipping view transition because document visibility state has become hidden." || e.message === "Skipping view transition because viewport size changed." || e.message === "Transition was aborted because of invalid state") && (e = null);
					}
					e !== null && c(e);
				} finally {
					r(), i(), o();
				}
			}), u.finished.finally(function() {
				for (var e = 0; e < d.length; e++) d[e].cancel();
				l.__reactViewTransition === u && (l.__reactViewTransition = null), s();
			}), u;
		} catch {
			return r(), i(), o(), null;
		}
	}
	function Np(e, t) {
		this._scope = document.documentElement, this._selector = "::view-transition-" + e + "(" + t + ")";
	}
	Np.prototype.animate = function(e, t) {
		return t = typeof t == "number" ? { duration: t } : D({}, t), t.pseudoElement = this._selector, this._scope.animate(e, t);
	}, Np.prototype.getAnimations = function() {
		for (var e = this._scope, t = this._selector, n = e.getAnimations({ subtree: !0 }), r = [], i = 0; i < n.length; i++) {
			var a = n[i].effect;
			a !== null && a.target === e && a.pseudoElement === t && r.push(n[i]);
		}
		return r;
	}, Np.prototype.getComputedStyle = function() {
		return getComputedStyle(this._scope, this._selector);
	};
	function Pp(e) {
		return {
			name: e,
			group: new Np("group", e),
			imagePair: new Np("image-pair", e),
			old: new Np("old", e),
			new: new Np("new", e)
		};
	}
	function Fp(e) {
		this._fragmentFiber = e, this._observers = this._eventListeners = null;
	}
	Fp.prototype.addEventListener = function(e, t, n) {
		var r = null, i = null;
		if (!(n != null && typeof n != "boolean" && (r = n.signal || null, r !== null && r.aborted))) {
			this._eventListeners === null && (this._eventListeners = []);
			var a = this._eventListeners;
			if (Bp(a, e, t, n) === -1) {
				var o = this, s = t;
				n != null && typeof n != "boolean" && !0 === n.once && (s = function(r) {
					o.removeEventListener(e, t, n), typeof t == "function" ? t.call(this, r) : t.handleEvent(r);
				}), r !== null && (i = o.removeEventListener.bind(o, e, t, n), r.addEventListener("abort", i, { once: !0 }), i = r.removeEventListener.bind(r, "abort", i)), r = Rp(n), a.push({
					type: e,
					listener: t,
					optionsOrUseCapture: n,
					attachedListener: s,
					cleanup: i
				}), h(this._fragmentFiber.child, !1, Ip, e, s, r);
			}
			this._eventListeners = a;
		}
	};
	function Ip(e, t, n, r) {
		return b(e).addEventListener(t, n, r), !1;
	}
	Fp.prototype.removeEventListener = function(e, t, n) {
		var r = this._eventListeners;
		if (r !== null && (t = Bp(r, e, t, n), t !== -1)) {
			var i = r[t];
			n = i.attachedListener;
			var a = i.cleanup;
			i = Rp(i.optionsOrUseCapture), h(this._fragmentFiber.child, !1, Lp, e, n, i), r.splice(t, 1), a !== null && a();
		}
	};
	function Lp(e, t, n, r) {
		return b(e).removeEventListener(t, n, r), !1;
	}
	function Rp(e) {
		return e != null && typeof e != "boolean" && (!0 === e.once || e.signal instanceof AbortSignal) ? {
			capture: e.capture,
			passive: e.passive
		} : e;
	}
	function zp(e) {
		return e == null ? "c=0" : typeof e == "boolean" ? "c=" + (e ? "1" : "0") : "c=" + (e.capture ? "1" : "0");
	}
	function Bp(e, t, n, r) {
		if (e.length === 0) return -1;
		r = zp(r);
		for (var i = 0; i < e.length; i++) {
			var a = e[i];
			if (a.type === t && a.listener === n && zp(a.optionsOrUseCapture) === r) return i;
		}
		return -1;
	}
	Fp.prototype.dispatchEvent = function(e) {
		var t = g(this._fragmentFiber);
		if (t === null) return !0;
		t = b(t);
		var n = this._eventListeners;
		if (n !== null && 0 < n.length || !e.bubbles) {
			var r = t.nodeType === 9 ? t.createComment("") : document.createTextNode("");
			if (n) for (var i = 0; i < n.length; i++) {
				var a = n[i];
				r.addEventListener(a.type, a.attachedListener, Rp(a.optionsOrUseCapture));
			}
			if (t.appendChild(r), e = r.dispatchEvent(e), n) for (i = 0; i < n.length; i++) a = n[i], r.removeEventListener(a.type, a.attachedListener, Rp(a.optionsOrUseCapture));
			return t.removeChild(r), e;
		}
		return t.dispatchEvent(e);
	}, Fp.prototype.focus = function(e) {
		h(this._fragmentFiber.child, !0, Vp, e, void 0, void 0);
	};
	function Vp(e, t) {
		return e.tag !== 6 && (e = b(e), pm(e, t));
	}
	Fp.prototype.focusLast = function(e) {
		var t = [];
		h(this._fragmentFiber.child, !0, Hp, t, void 0, void 0);
		for (var n = t.length - 1; 0 <= n && !Vp(t[n], e); n--);
	};
	function Hp(e, t) {
		return t.push(e), !1;
	}
	Fp.prototype.blur = function() {
		var e = g(this._fragmentFiber);
		e !== null && (e = b(e), e = lp(e).activeElement, e !== null && h(this._fragmentFiber.child, !1, Up, e, void 0, void 0));
	};
	function Up(e, t) {
		return e.tag !== 6 && (e = b(e), e === t || e.contains(t) ? (t.blur(), !0) : !1);
	}
	Fp.prototype.observeUsing = function(e) {
		this._observers === null && (this._observers = /* @__PURE__ */ new Set()), this._observers.add(e), h(this._fragmentFiber.child, !1, Wp, e, void 0, void 0);
	};
	function Wp(e, t) {
		return e.tag !== 6 && (e = b(e), t.observe(e), !1);
	}
	Fp.prototype.unobserveUsing = function(e) {
		var t = this._observers;
		if (t !== null && t.has(e)) {
			t.delete(e), h(this._fragmentFiber.child, !1, Gp, e, void 0, void 0);
			for (var n = t = 0; n < Kp.length; n++) {
				var r = Kp[n];
				r.fragmentInstance === this && r.observer === e ? e.unobserve(r.instance) : Kp[t++] = r;
			}
			Kp.length = t;
		}
	};
	function Gp(e, t) {
		return e.tag !== 6 && (e = b(e), t.unobserve(e), !1);
	}
	var Kp = [], qp = !1;
	function Jp(e, t, n) {
		Kp.push({
			fragmentInstance: e,
			observer: t,
			instance: n
		}), qp || (qp = !0, mm(function() {
			qp = !1;
			var e = Kp;
			Kp = [];
			for (var t = 0; t < e.length; t++) {
				var n = e[t];
				n.observer.unobserve(n.instance);
			}
		}));
	}
	Fp.prototype.getClientRects = function() {
		var e = [];
		return h(this._fragmentFiber.child, !1, Yp, e, void 0, void 0), e;
	};
	function Yp(e, t) {
		if (e.tag === 6) {
			e = e.stateNode;
			var n = e.ownerDocument.createRange();
			n.selectNodeContents(e), t.push.apply(t, n.getClientRects());
		} else e = b(e), t.push.apply(t, e.getClientRects());
		return !1;
	}
	Fp.prototype.getRootNode = function(e) {
		var t = g(this._fragmentFiber);
		return t === null ? this : b(t).getRootNode(e);
	}, Fp.prototype.compareDocumentPosition = function(e) {
		var t = g(this._fragmentFiber);
		if (t === null) return Node.DOCUMENT_POSITION_DISCONNECTED;
		var n = [];
		h(this._fragmentFiber.child, !1, Hp, n, void 0, void 0);
		var r = b(t);
		if (n.length === 0) {
			if (n = r, _(this._fragmentFiber)) {
				a: {
					for (t = this._fragmentFiber.return; t !== null;) {
						if (t.tag === 4) {
							t = t.stateNode.containerInfo;
							break a;
						}
						if (t.tag === 3 || t.tag === 5 || t.tag === 27) break;
						t = t.return;
					}
					t = null;
				}
				t != null && (n = t);
			}
			t = this._fragmentFiber;
			var i = r = n.compareDocumentPosition(e);
			return n === e ? i = Node.DOCUMENT_POSITION_CONTAINS : r & Node.DOCUMENT_POSITION_CONTAINED_BY && (n = v(t)[1], n === null ? i = Node.DOCUMENT_POSITION_PRECEDING : (e = b(n).compareDocumentPosition(e), i = e === 0 || e & Node.DOCUMENT_POSITION_FOLLOWING ? Node.DOCUMENT_POSITION_FOLLOWING : Node.DOCUMENT_POSITION_PRECEDING)), i |= Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC;
		}
		t = b(n[0]), i = b(n[n.length - 1]);
		var a = _(this._fragmentFiber) ? t.parentElement : r;
		if (a == null) return Node.DOCUMENT_POSITION_DISCONNECTED;
		r = a.compareDocumentPosition(t) & Node.DOCUMENT_POSITION_CONTAINED_BY, a = a.compareDocumentPosition(i) & Node.DOCUMENT_POSITION_CONTAINED_BY;
		var o = t.compareDocumentPosition(e), s = i.compareDocumentPosition(e), c = o & Node.DOCUMENT_POSITION_CONTAINED_BY || s & Node.DOCUMENT_POSITION_CONTAINED_BY;
		return s = r && a && o & Node.DOCUMENT_POSITION_FOLLOWING && s & Node.DOCUMENT_POSITION_PRECEDING, t = r && t === e || a && i === e || c || s ? Node.DOCUMENT_POSITION_CONTAINED_BY : !r && t === e || !a && i === e ? Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC : o, t & Node.DOCUMENT_POSITION_DISCONNECTED || t & Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC || Xp(t, this._fragmentFiber, n[0], n[n.length - 1], e) ? t : Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC;
	};
	function Xp(e, t, n, r, i) {
		var a = Pt(i);
		if (e & Node.DOCUMENT_POSITION_CONTAINED_BY) {
			if (n = !!a) a: {
				for (; a !== null;) {
					if (a.tag === 7 && (a === t || a.alternate === t)) {
						n = !0;
						break a;
					}
					a = a.return;
				}
				n = !1;
			}
			return n;
		}
		if (e & Node.DOCUMENT_POSITION_CONTAINS) {
			if (a === null) return a = i.ownerDocument, i === a || i === a.documentElement || i === a.body;
			a: {
				for (a = t, t = g(t); a !== null;) {
					if (!(a.tag !== 5 && a.tag !== 3 && a.tag !== 27 || a !== t && a.alternate !== t)) {
						a = !0;
						break a;
					}
					a = a.return;
				}
				a = !1;
			}
			return a;
		}
		return e & Node.DOCUMENT_POSITION_PRECEDING ? ((t = !!a) && !(t = a === n) && (t = E(n, a, T), t === null ? t = !1 : (h(t, !0, C, a, n), a = x, x = null, t = a !== null)), t) : e & Node.DOCUMENT_POSITION_FOLLOWING ? ((t = !!a) && !(t = a === r) && (t = E(r, a, T), t === null ? t = !1 : (h(t, !0, w, a, r), a = x, S = x = null, t = a !== null)), t) : !1;
	}
	function Zp(e, t) {
		var n = e.ownerDocument.createRange();
		n.selectNodeContents(e), e = n.getBoundingClientRect(), window.scrollTo(window.scrollX + e.left, t ? window.scrollY + e.top : window.scrollY + e.bottom - window.innerHeight);
	}
	Fp.prototype.scrollIntoView = function(e) {
		if (typeof e == "object") throw Error(i(566));
		var t = [];
		h(this._fragmentFiber.child, !1, Hp, t, void 0, void 0);
		var n = !1 !== e;
		if (t.length === 0) {
			var r = v(this._fragmentFiber);
			if (r = n ? r[1] || r[0] || g(this._fragmentFiber) : r[0] || r[1], r === null) return;
			if (r.tag === 6) {
				e = b(r), Zp(e, n);
				return;
			}
			if (r = b(r), r.nodeType !== 9) {
				if (r.nodeType === 11) {
					n = "host" in r ? r.host : null, n !== null && n.scrollIntoView(e);
					return;
				}
				r.scrollIntoView(e);
			}
		}
		for (r = n ? t.length - 1 : 0; r !== (n ? -1 : t.length);) {
			var a = t[r];
			a.tag === 6 ? (a = b(a), Zp(a, n)) : b(a).scrollIntoView(e), r += n ? -1 : 1;
		}
	};
	function Qp(e, t) {
		return e = b(e), $p(e, t), !1;
	}
	function $p(e, t) {
		e.reactFragments ??= /* @__PURE__ */ new Set(), e.reactFragments.add(t);
	}
	function em(e, t) {
		var n = t._eventListeners;
		if (n !== null) for (var r = 0; r < n.length; r++) {
			var i = n[r];
			e.addEventListener(i.type, i.attachedListener, Rp(i.optionsOrUseCapture));
		}
		e.nodeType !== 3 && (n = t._observers, n !== null && n.forEach(function(n) {
			for (var r = 0, i = 0; i < Kp.length; i++) {
				var a = Kp[i];
				(a.fragmentInstance !== t || a.observer !== n || a.instance !== e) && (Kp[r++] = a);
			}
			Kp.length = r, n.observe(e);
		}), $p(e, t));
	}
	function tm(e, t) {
		var n = t._eventListeners;
		if (n !== null) for (var r = 0; r < n.length; r++) {
			var i = n[r];
			e.removeEventListener(i.type, i.attachedListener, Rp(i.optionsOrUseCapture));
		}
		e.nodeType !== 3 && (n = t._observers, n !== null && n.forEach(function(n) {
			typeof n.rootMargin == "string" ? Jp(t, n, e) : n.unobserve(e);
		}), e.reactFragments != null && e.reactFragments.delete(t));
	}
	function nm(e) {
		var t = e.firstChild;
		for (t && t.nodeType === 10 && (t = t.nextSibling); t;) {
			var n = t;
			switch (t = t.nextSibling, n.nodeName) {
				case "HTML":
				case "HEAD":
				case "BODY":
					nm(n), Nt(n);
					continue;
				case "SCRIPT":
				case "STYLE": continue;
				case "LINK": if (n.rel.toLowerCase() === "stylesheet") continue;
			}
			e.removeChild(n);
		}
	}
	function rm(e, t, n, r) {
		for (; e.nodeType === 1;) {
			var i = n;
			if (e.nodeName.toLowerCase() !== t.toLowerCase()) {
				if (!r && (e.nodeName !== "INPUT" || e.type !== "hidden")) break;
			} else if (!r) {
				if (t === "input" && e.type === "hidden") {
					var a = i.name == null ? null : "" + i.name;
					if (i.type === "hidden" && e.getAttribute("name") === a) return e;
				} else return e;
			} else if (!e[jt]) switch (t) {
				case "meta":
					if (!e.hasAttribute("itemprop")) break;
					return e;
				case "link":
					if (a = e.getAttribute("rel"), a === "stylesheet" && e.hasAttribute("data-precedence") || a !== i.rel || e.getAttribute("href") !== (i.href == null || i.href === "" ? null : i.href) || e.getAttribute("crossorigin") !== (i.crossOrigin == null ? null : i.crossOrigin) || e.getAttribute("title") !== (i.title == null ? null : i.title)) break;
					return e;
				case "style":
					if (e.hasAttribute("data-precedence")) break;
					return e;
				case "script":
					if (a = e.getAttribute("src"), (a !== (i.src == null ? null : i.src) || e.getAttribute("type") !== (i.type == null ? null : i.type) || e.getAttribute("crossorigin") !== (i.crossOrigin == null ? null : i.crossOrigin)) && a && e.hasAttribute("async") && !e.hasAttribute("itemprop")) break;
					return e;
				default: return e;
			}
			if (e = lm(e.nextSibling), e === null) break;
		}
		return null;
	}
	function im(e, t, n) {
		if (t === "") return null;
		for (; e.nodeType !== 3;) if ((e.nodeType !== 1 || e.nodeName !== "INPUT" || e.type !== "hidden") && !n || (e = lm(e.nextSibling), e === null)) return null;
		return e;
	}
	function am(e, t) {
		for (; e.nodeType !== 8;) if ((e.nodeType !== 1 || e.nodeName !== "INPUT" || e.type !== "hidden") && !t || (e = lm(e.nextSibling), e === null)) return null;
		return e;
	}
	function om(e) {
		return e.data === "$?" || e.data === "$~";
	}
	function sm(e) {
		return e.data === "$!" || e.data === "$?" && e.ownerDocument.readyState !== "loading";
	}
	function cm(e, t) {
		var n = e.ownerDocument;
		if (e.data === "$~") e._reactRetry = t;
		else if (e.data !== "$?" || n.readyState !== "loading") t();
		else {
			var r = function() {
				t(), n.removeEventListener("DOMContentLoaded", r);
			};
			n.addEventListener("DOMContentLoaded", r), e._reactRetry = r;
		}
	}
	function lm(e) {
		for (; e != null; e = e.nextSibling) {
			var t = e.nodeType;
			if (t === 1 || t === 3) break;
			if (t === 8) {
				if (t = e.data, t === "$" || t === "$!" || t === "$?" || t === "$~" || t === "&" || t === "F!" || t === "F") break;
				if (t === "/$" || t === "/&") return null;
			}
		}
		return e;
	}
	var um = null;
	function dm(e) {
		e = e.nextSibling;
		for (var t = 0; e;) {
			if (e.nodeType === 8) {
				var n = e.data;
				if (n === "/$" || n === "/&") {
					if (t === 0) return lm(e.nextSibling);
					t--;
				} else n !== "$" && n !== "$!" && n !== "$?" && n !== "$~" && n !== "&" || t++;
			}
			e = e.nextSibling;
		}
		return null;
	}
	function fm(e) {
		e = e.previousSibling;
		for (var t = 0; e;) {
			if (e.nodeType === 8) {
				var n = e.data;
				if (n === "$" || n === "$!" || n === "$?" || n === "$~" || n === "&") {
					if (t === 0) return e;
					t--;
				} else n !== "/$" && n !== "/&" || t++;
			}
			e = e.previousSibling;
		}
		return null;
	}
	function pm(e, t) {
		function n() {
			r = !0;
		}
		if (e.ownerDocument.activeElement === e) return !0;
		var r = !1;
		try {
			e.ownerDocument.addEventListener("focus", n, !0), (e.focus || HTMLElement.prototype.focus).call(e, t);
		} finally {
			e.ownerDocument.removeEventListener("focus", n, !0);
		}
		return r;
	}
	function mm(e) {
		yp(function() {
			yp(function(t) {
				return e(t);
			});
		});
	}
	function hm(e, t, n) {
		switch (t = lp(n), e) {
			case "html":
				if (e = t.documentElement, !e) throw Error(i(452));
				return e;
			case "head":
				if (e = t.head, !e) throw Error(i(453));
				return e;
			case "body":
				if (e = t.body, !e) throw Error(i(454));
				return e;
			default: throw Error(i(451));
		}
	}
	function gm(e, t, n) {
		for (var r in n) {
			var i = n[r];
			n.hasOwnProperty(r) && i != null && $(e, t, r, null, rp, i);
		}
		n.dangerouslySetInnerHTML != null && (e.textContent = ""), e.onclick === bn && (e.onclick = null), Nt(e);
	}
	function _m(e) {
		for (var t = e.attributes; t.length;) e.removeAttributeNode(t[0]);
		Nt(e);
	}
	var vm = /* @__PURE__ */ new Map(), ym = /* @__PURE__ */ new Set();
	function bm(e) {
		if (typeof e.getRootNode == "function") {
			var t = e.getRootNode();
			if (t.nodeType === 9 || t.nodeType === 11) return t;
		}
		return e.nodeType === 9 ? e : e.ownerDocument;
	}
	var xm = P.d;
	P.d = {
		f: Sm,
		r: Cm,
		D: Em,
		C: Dm,
		L: Om,
		m: km,
		X: jm,
		S: Am,
		M: Mm
	};
	function Sm() {
		var e = xm.f(), t = zd();
		return e || t;
	}
	function Cm(e) {
		var t = Ft(e);
		t !== null && t.tag === 5 && t.type === "form" ? ic(t) : xm.r(e);
	}
	var wm = typeof document > "u" ? null : document;
	function Tm(e, t, n) {
		var r = wm;
		if (r && typeof t == "string" && t) {
			var i = an(t);
			i = "link[rel=\"" + e + "\"][href=\"" + i + "\"]", typeof n == "string" && (i += "[crossorigin=\"" + n + "\"]"), ym.has(i) || (ym.add(i), e = {
				rel: e,
				crossOrigin: n,
				href: t
			}, r.querySelector(i) === null && (t = r.createElement("link"), np(t, "link", e), Rt(t), r.head.appendChild(t)));
		}
	}
	function Em(e) {
		xm.D(e), Tm("dns-prefetch", e, null);
	}
	function Dm(e, t) {
		xm.C(e, t), Tm("preconnect", e, t);
	}
	function Om(e, t, n) {
		xm.L(e, t, n);
		var r = wm;
		if (r && e && t) {
			var i = "link[rel=\"preload\"][as=\"" + an(t) + "\"]";
			t === "image" && n && n.imageSrcSet ? (i += "[imagesrcset=\"" + an(n.imageSrcSet) + "\"]", typeof n.imageSizes == "string" && (i += "[imagesizes=\"" + an(n.imageSizes) + "\"]")) : i += "[href=\"" + an(e) + "\"]";
			var a = i;
			switch (t) {
				case "style":
					a = Pm(e);
					break;
				case "script": a = Rm(e);
			}
			if (!(vm.has(a) || (e = D({
				rel: "preload",
				href: t === "image" && n && n.imageSrcSet ? void 0 : e,
				as: t
			}, n), vm.set(a, e), r.querySelector(i) !== null || t === "style" && r.querySelector(Fm(a)) || t === "script" && r.querySelector(zm(a))))) {
				var o = r.createElement("link");
				np(o, "link", e), t === "style" && (o[Mt] = !0, o.onload = o.onerror = function() {
					zt(o);
				}), Rt(o), r.head.appendChild(o);
			}
		}
	}
	function km(e, t) {
		xm.m(e, t);
		var n = wm;
		if (n && e) {
			var r = t && typeof t.as == "string" ? t.as : "script", i = "link[rel=\"modulepreload\"][as=\"" + an(r) + "\"][href=\"" + an(e) + "\"]", a = i;
			switch (r) {
				case "audioworklet":
				case "paintworklet":
				case "serviceworker":
				case "sharedworker":
				case "worker":
				case "script": a = Rm(e);
			}
			if (!vm.has(a) && (e = D({
				rel: "modulepreload",
				href: e
			}, t), vm.set(a, e), n.querySelector(i) === null)) {
				switch (r) {
					case "audioworklet":
					case "paintworklet":
					case "serviceworker":
					case "sharedworker":
					case "worker":
					case "script": if (n.querySelector(zm(a))) return;
				}
				r = n.createElement("link"), np(r, "link", e), Rt(r), n.head.appendChild(r);
			}
		}
	}
	function Am(e, t, n) {
		xm.S(e, t, n);
		var r = wm;
		if (r && e) {
			var i = Lt(r).hoistableStyles, a = Pm(e);
			t ||= "default";
			var o = i.get(a);
			if (!o) {
				var s = {
					loading: 0,
					preload: null
				};
				if (o = r.querySelector(Fm(a))) s.loading = 5;
				else {
					e = D({
						rel: "stylesheet",
						href: e,
						"data-precedence": t
					}, n), (n = vm.get(a)) && Hm(e, n);
					var c = o = r.createElement("link");
					Rt(c), np(c, "link", e), c._p = new Promise(function(e, t) {
						c.onload = e, c.onerror = t;
					}), c.addEventListener("load", function() {
						s.loading |= 1;
					}), c.addEventListener("error", function() {
						s.loading |= 2;
					}), s.loading |= 4, Vm(o, t, r);
				}
				o = {
					type: "stylesheet",
					instance: o,
					count: 1,
					state: s
				}, i.set(a, o);
			}
		}
	}
	function jm(e, t) {
		xm.X(e, t);
		var n = wm;
		if (n && e) {
			var r = Lt(n).hoistableScripts, i = Rm(e), a = r.get(i);
			a || (a = n.querySelector(zm(i)), a || (e = D({
				src: e,
				async: !0
			}, t), (t = vm.get(i)) && Um(e, t), a = n.createElement("script"), Rt(a), np(a, "link", e), n.head.appendChild(a)), a = {
				type: "script",
				instance: a,
				count: 1,
				state: null
			}, r.set(i, a));
		}
	}
	function Mm(e, t) {
		xm.M(e, t);
		var n = wm;
		if (n && e) {
			var r = Lt(n).hoistableScripts, i = Rm(e), a = r.get(i);
			a || (a = n.querySelector(zm(i)), a || (e = D({
				src: e,
				async: !0,
				type: "module"
			}, t), (t = vm.get(i)) && Um(e, t), a = n.createElement("script"), Rt(a), np(a, "link", e), n.head.appendChild(a)), a = {
				type: "script",
				instance: a,
				count: 1,
				state: null
			}, r.set(i, a));
		}
	}
	function Nm(e, t, n, r) {
		var a = (a = Te.current) ? bm(a) : null;
		if (!a) throw Error(i(446));
		switch (e) {
			case "meta":
			case "title": return null;
			case "style": return typeof n.precedence == "string" && typeof n.href == "string" ? (n = Pm(n.href), t = Lt(a).hoistableStyles, r = t.get(n), r || (r = {
				type: "style",
				instance: null,
				count: 0,
				state: null
			}, t.set(n, r)), r) : {
				type: "void",
				instance: null,
				count: 0,
				state: null
			};
			case "link":
				if (n.rel === "stylesheet" && typeof n.href == "string" && typeof n.precedence == "string") {
					e = Pm(n.href);
					var o = Lt(a).hoistableStyles, s = o.get(e);
					if (s || (a = a.ownerDocument || a, s = {
						type: "stylesheet",
						instance: null,
						count: 0,
						state: {
							loading: 0,
							preload: null
						}
					}, o.set(e, s), (o = a.querySelector(Fm(e))) ? o._p || (s.instance = o, s.state.loading = 5) : (o = vm.get(e), o || (o = {
						rel: "preload",
						as: "style",
						href: n.href,
						crossOrigin: n.crossOrigin,
						integrity: n.integrity,
						media: n.media,
						hrefLang: n.hrefLang,
						referrerPolicy: n.referrerPolicy
					}, vm.set(e, o)), Lm(a, e, o, s.state))), t && r === null) throw Error(i(528, ""));
					return s;
				}
				if (t && r !== null) throw Error(i(529, ""));
				return null;
			case "script": return t = n.async, n = n.src, typeof n == "string" && t && typeof t != "function" && typeof t != "symbol" ? (n = Rm(n), t = Lt(a).hoistableScripts, r = t.get(n), r || (r = {
				type: "script",
				instance: null,
				count: 0,
				state: null
			}, t.set(n, r)), r) : {
				type: "void",
				instance: null,
				count: 0,
				state: null
			};
			default: throw Error(i(444, e));
		}
	}
	function Pm(e) {
		return "href=\"" + an(e) + "\"";
	}
	function Fm(e) {
		return "link[rel=\"stylesheet\"][" + e + "]";
	}
	function Im(e) {
		return D({}, e, {
			"data-precedence": e.precedence,
			precedence: null
		});
	}
	function Lm(e, t, n, r) {
		if (t = e.querySelector("link[rel=\"preload\"][as=\"style\"][" + t + "]")) {
			if (!0 !== t[Mt]) {
				r.loading = 1;
				return;
			}
		} else t = e.createElement("link"), t[Mt] = !0, t.onload = t.onerror = zt.bind(null, t), np(t, "link", n), Rt(t), e.head.appendChild(t);
		r.preload = t, t.addEventListener("load", function() {
			return r.loading |= 1;
		}), t.addEventListener("error", function() {
			return r.loading |= 2;
		});
	}
	function Rm(e) {
		return "[src=\"" + an(e) + "\"]";
	}
	function zm(e) {
		return "script[async]" + e;
	}
	function Bm(e, t, n) {
		if (t.count++, t.instance === null) switch (t.type) {
			case "style":
				var r = e.querySelector("style[data-href~=\"" + an(n.href) + "\"]");
				if (r) return t.instance = r, Rt(r), r;
				var a = D({}, n, {
					"data-href": n.href,
					"data-precedence": n.precedence,
					href: null,
					precedence: null
				});
				return r = (e.ownerDocument || e).createElement("style"), Rt(r), np(r, "style", a), Vm(r, n.precedence, e), t.instance = r;
			case "stylesheet":
				a = Pm(n.href);
				var o = e.querySelector(Fm(a));
				if (o) return t.state.loading |= 4, t.instance = o, Rt(o), o;
				r = Im(n), (a = vm.get(a)) && Hm(r, a), o = (e.ownerDocument || e).createElement("link"), Rt(o);
				var s = o;
				return s._p = new Promise(function(e, t) {
					s.onload = e, s.onerror = t;
				}), np(o, "link", r), t.state.loading |= 4, Vm(o, n.precedence, e), t.instance = o;
			case "script": return o = Rm(n.src), (a = e.querySelector(zm(o))) ? (t.instance = a, Rt(a), a) : (r = n, (a = vm.get(o)) && (r = D({}, n), Um(r, a)), e = e.ownerDocument || e, a = e.createElement("script"), Rt(a), np(a, "link", r), e.head.appendChild(a), t.instance = a);
			case "void": return null;
			default: throw Error(i(443, t.type));
		}
		else t.type === "stylesheet" && !(t.state.loading & 4) && (r = t.instance, t.state.loading |= 4, Vm(r, n.precedence, e));
		return t.instance;
	}
	function Vm(e, t, n) {
		for (var r = n.querySelectorAll("link[rel=\"stylesheet\"][data-precedence],style[data-precedence]"), i = r.length ? r[r.length - 1] : null, a = i, o = 0; o < r.length; o++) {
			var s = r[o];
			if (s.dataset.precedence === t) a = s;
			else if (a !== i) break;
		}
		a ? a.parentNode.insertBefore(e, a.nextSibling) : (t = n.nodeType === 9 ? n.head : n, t.insertBefore(e, t.firstChild));
	}
	function Hm(e, t) {
		e.crossOrigin ??= t.crossOrigin, e.referrerPolicy ??= t.referrerPolicy, e.title ??= t.title;
	}
	function Um(e, t) {
		e.crossOrigin ??= t.crossOrigin, e.referrerPolicy ??= t.referrerPolicy, e.integrity ??= t.integrity;
	}
	var Wm = null;
	function Gm(e, t, n) {
		if (Wm === null) {
			var r = /* @__PURE__ */ new Map(), i = Wm = /* @__PURE__ */ new Map();
			i.set(n, r);
		} else i = Wm, r = i.get(n), r || (r = /* @__PURE__ */ new Map(), i.set(n, r));
		if (r.has(e)) return r;
		for (r.set(e, null), n = n.getElementsByTagName(e), i = 0; i < n.length; i++) {
			var a = n[i];
			if (!(a[jt] || a[I] || e === "link" && a.getAttribute("rel") === "stylesheet") && a.namespaceURI !== "http://www.w3.org/2000/svg") {
				var o = a.getAttribute(t) || "";
				o = e + o;
				var s = r.get(o);
				s ? s.push(a) : r.set(o, [a]);
			}
		}
		return r;
	}
	function Km(e, t, n) {
		e = e.ownerDocument || e, e.head.insertBefore(n, t === "title" ? e.querySelector("head > title") : null);
	}
	function qm(e, t, n) {
		if (n === 1 || t.itemProp != null) return !1;
		switch (e) {
			case "meta":
			case "title": return !0;
			case "style":
				if (typeof t.precedence != "string" || typeof t.href != "string" || t.href === "") break;
				return !0;
			case "link":
				if (typeof t.rel != "string" || typeof t.href != "string" || t.href === "" || t.onLoad || t.onError) break;
				switch (t.rel) {
					case "stylesheet": return e = t.disabled, typeof t.precedence == "string" && e == null;
					default: return !0;
				}
			case "script": if (t.async && typeof t.async != "function" && typeof t.async != "symbol" && !t.onLoad && !t.onError && t.src && typeof t.src == "string") return !0;
		}
		return !1;
	}
	function Jm(e, t) {
		return e === "img" && t.src != null && t.src !== "" && t.onLoad == null && t.loading !== "lazy";
	}
	function Ym(e) {
		return !(e.type === "stylesheet" && !(e.state.loading & 3));
	}
	function Xm(e) {
		return (e.width || 100) * (e.height || 100) * (typeof devicePixelRatio == "number" ? devicePixelRatio : 1) * .25;
	}
	function Zm(e, t) {
		typeof t.decode == "function" && (e.imgCount++, t.complete || (e.imgBytes += Xm(t), e.suspenseyImages.push(t)), e = rh.bind(e), t.decode().then(e, e));
	}
	function Qm(e, t, n, r) {
		if (n.type === "stylesheet" && (typeof r.media != "string" || !1 !== matchMedia(r.media).matches) && !(n.state.loading & 4)) {
			if (n.instance === null) {
				var i = Pm(r.href), a = t.querySelector(Fm(i));
				if (a) {
					t = a._p, typeof t == "object" && t && typeof t.then == "function" && (e.count++, e = nh.bind(e), t.then(e, e)), n.state.loading |= 4, n.instance = a, Rt(a);
					return;
				}
				a = t.ownerDocument || t, r = Im(r), (i = vm.get(i)) && Hm(r, i), a = a.createElement("link"), Rt(a);
				var o = a;
				o._p = new Promise(function(e, t) {
					o.onload = e, o.onerror = t;
				}), np(a, "link", r), n.instance = a;
			}
			e.stylesheets === null && (e.stylesheets = /* @__PURE__ */ new Map()), e.stylesheets.set(n, t), (t = n.state.preload) && !(n.state.loading & 3) && (e.count++, n = nh.bind(e), t.addEventListener("load", n), t.addEventListener("error", n));
		}
	}
	var $m = 0;
	function eh(e, t) {
		return e.stylesheets && e.count === 0 && ah(e, e.stylesheets), 0 < e.count || 0 < e.imgCount ? function(n) {
			var r = setTimeout(function() {
				if (e.stylesheets && ah(e, e.stylesheets), e.unsuspend) {
					var t = e.unsuspend;
					e.unsuspend = null, t();
				}
			}, 6e4 + t);
			0 < e.imgBytes && $m === 0 && ($m = 62500 * op());
			var i = setTimeout(function() {
				if (e.waitingForImages = !1, e.count === 0 && (e.stylesheets && ah(e, e.stylesheets), e.unsuspend)) {
					var t = e.unsuspend;
					e.unsuspend = null, t();
				}
			}, (e.imgBytes > $m ? 50 : 800) + t);
			return e.unsuspend = n, function() {
				e.unsuspend = null, clearTimeout(r), clearTimeout(i);
			};
		} : null;
	}
	function th(e) {
		if (e.count === 0 && (e.imgCount === 0 || !e.waitingForImages)) {
			if (e.stylesheets) ah(e, e.stylesheets);
			else if (e.unsuspend) {
				var t = e.unsuspend;
				e.unsuspend = null, t();
			}
		}
	}
	function nh() {
		this.count--, th(this);
	}
	function rh() {
		this.imgCount--, th(this);
	}
	var ih = null;
	function ah(e, t) {
		e.stylesheets = null, e.unsuspend !== null && (e.count++, ih = /* @__PURE__ */ new Map(), t.forEach(oh, e), ih = null, nh.call(e));
	}
	function oh(e, t) {
		if (!(t.state.loading & 4)) {
			var n = ih.get(e);
			if (n) var r = n.get(null);
			else {
				n = /* @__PURE__ */ new Map(), ih.set(e, n);
				for (var i = e.querySelectorAll("link[data-precedence],style[data-precedence]"), a = 0; a < i.length; a++) {
					var o = i[a];
					(o.nodeName === "LINK" || o.getAttribute("media") !== "not all") && (n.set(o.dataset.precedence, o), r = o);
				}
				r && n.set(null, r);
			}
			i = t.instance, o = i.getAttribute("data-precedence"), a = n.get(o) || r, a === r && n.set(null, i), n.set(o, i), this.count++, r = nh.bind(this), i.addEventListener("load", r), i.addEventListener("error", r), a ? a.parentNode.insertBefore(i, a.nextSibling) : (e = e.nodeType === 9 ? e.head : e, e.insertBefore(i, e.firstChild)), t.state.loading |= 4;
		}
	}
	var sh = {
		$$typeof: j,
		Provider: null,
		Consumer: null,
		_currentValue: ve,
		_currentValue2: ve,
		_threadCount: 0
	};
	function ch(e, t, n, r, i, a, o, s, c) {
		this.tag = 1, this.containerInfo = e, this.pingCache = this.current = this.pendingChildren = null, this.timeoutHandle = -1, this.callbackNode = this.next = this.pendingContext = this.context = this.cancelPendingCommit = null, this.callbackPriority = 0, this.expirationTimes = mt(-1), this.entangledLanes = this.shellSuspendCounter = this.errorRecoveryDisabledLanes = this.expiredLanes = this.warmLanes = this.pingedLanes = this.suspendedLanes = this.pendingLanes = 0, this.entanglements = mt(0), this.hiddenUpdates = mt(null), this.identifierPrefix = r, this.onUncaughtError = i, this.onCaughtError = a, this.onRecoverableError = o, this.pooledCache = null, this.pooledCacheLanes = 0, this.formState = c, this.transitionTypes = null, this.incompleteTransitions = /* @__PURE__ */ new Map();
	}
	function lh(e, t, n, r, i, a, o, s, c, l, u, d) {
		return e = new ch(e, t, n, o, c, l, u, d, s), t = 1, !0 === a && (t |= 24), a = Pi(3, null, null, t), e.current = a, a.stateNode = e, t = Na(), t.refCount++, e.pooledCache = t, t.refCount++, a.memoizedState = {
			element: r,
			isDehydrated: n,
			cache: t
		}, go(a), e;
	}
	function uh(e) {
		return e ? (e = Mi, e) : Mi;
	}
	function dh(e, t, n, r, i, a) {
		i = uh(i), r.context === null ? r.context = i : r.pendingContext = i, r = vo(t), r.payload = { element: n }, a = a === void 0 ? null : a, a !== null && (r.callback = a), n = yo(e, r, t), n !== null && (Pd(n, e, t), bo(n, e, t));
	}
	function fh(e, t) {
		if (e = e.memoizedState, e !== null && e.dehydrated !== null) {
			var n = e.retryLane;
			e.retryLane = n !== 0 && n < t ? n : t;
		}
	}
	function ph(e, t) {
		fh(e, t), (e = e.alternate) && fh(e, t);
	}
	function mh(e) {
		if (e.tag === 13 || e.tag === 31) {
			var t = ki(e, 67108864);
			t !== null && Pd(t, e, 67108864), ph(e, 67108864);
		}
	}
	function hh(e) {
		if (e.tag === 13 || e.tag === 31) {
			var t = jd();
			t = bt(t);
			var n = ki(e, t);
			n !== null && Pd(n, e, t), ph(e, t);
		}
	}
	var gh = !0;
	function _h(e, t, n, r) {
		var i = N.T;
		N.T = null;
		var a = P.p;
		try {
			P.p = 2, yh(e, t, n, r);
		} finally {
			P.p = a, N.T = i;
		}
	}
	function vh(e, t, n, r) {
		var i = N.T;
		N.T = null;
		var a = P.p;
		try {
			P.p = 8, yh(e, t, n, r);
		} finally {
			P.p = a, N.T = i;
		}
	}
	function yh(e, t, n, r) {
		if (gh) {
			var i = bh(r);
			if (i === null) Kf(e, t, r, xh, n), Mh(e, r);
			else if (Ph(i, e, t, n, r)) r.stopPropagation();
			else if (Mh(e, r), t & 4 && -1 < jh.indexOf(e)) {
				for (; i !== null;) {
					var a = Ft(i);
					if (a !== null) switch (a.tag) {
						case 3:
							if (a = a.stateNode, a.current.memoizedState.isDehydrated) {
								var o = ct(a.pendingLanes);
								if (o !== 0) {
									var s = a;
									for (s.pendingLanes |= 2, s.entangledLanes |= 2; o;) {
										var c = 1 << 31 - tt(o);
										s.entanglements[1] |= c, o &= ~c;
									}
									Ef(a), !(G & 6) && (_d = Ue() + 500, Df(0, !1));
								}
							}
							break;
						case 31:
						case 13: s = ki(a, 2), s !== null && Pd(s, a, 2), zd(), ph(a, 2);
					}
					if (a = bh(r), a === null && Kf(e, t, r, xh, n), a === i) break;
					i = a;
				}
				i !== null && r.stopPropagation();
			} else Kf(e, t, r, null, n);
		}
	}
	function bh(e) {
		return e = Sn(e), Sh(e);
	}
	var xh = null;
	function Sh(e) {
		if (xh = null, e = Pt(e), e !== null) {
			var t = o(e);
			if (t === null) e = null;
			else {
				var n = t.tag;
				if (n === 13) {
					if (e = s(t), e !== null) return e;
					e = null;
				} else if (n === 31) {
					if (e = c(t), e !== null) return e;
					e = null;
				} else if (n === 3) {
					if (t.stateNode.current.memoizedState.isDehydrated) return t.tag === 3 ? t.stateNode.containerInfo : null;
					e = null;
				} else t !== e && (e = null);
			}
		}
		return xh = e, null;
	}
	function Ch(e) {
		switch (e) {
			case "beforetoggle":
			case "cancel":
			case "click":
			case "close":
			case "contextmenu":
			case "copy":
			case "cut":
			case "auxclick":
			case "dblclick":
			case "dragend":
			case "dragstart":
			case "drop":
			case "focusin":
			case "focusout":
			case "input":
			case "invalid":
			case "keydown":
			case "keypress":
			case "keyup":
			case "mousedown":
			case "mouseup":
			case "paste":
			case "pause":
			case "play":
			case "pointercancel":
			case "pointerdown":
			case "pointerup":
			case "ratechange":
			case "reset":
			case "seeked":
			case "submit":
			case "toggle":
			case "touchcancel":
			case "touchend":
			case "touchstart":
			case "volumechange":
			case "change":
			case "selectionchange":
			case "textInput":
			case "compositionstart":
			case "compositionend":
			case "compositionupdate":
			case "beforeblur":
			case "afterblur":
			case "beforeinput":
			case "blur":
			case "fullscreenchange":
			case "fullscreenerror":
			case "focus":
			case "hashchange":
			case "popstate":
			case "select":
			case "selectstart": return 2;
			case "drag":
			case "dragenter":
			case "dragexit":
			case "dragleave":
			case "dragover":
			case "mousemove":
			case "mouseout":
			case "mouseover":
			case "pointermove":
			case "pointerout":
			case "pointerover":
			case "resize":
			case "scroll":
			case "touchmove":
			case "wheel":
			case "mouseenter":
			case "mouseleave":
			case "pointerenter":
			case "pointerleave": return 8;
			case "message": switch (We()) {
				case Ge: return 2;
				case Ke: return 8;
				case qe:
				case Je: return 32;
				case Ye: return 268435456;
				default: return 32;
			}
			default: return 32;
		}
	}
	var wh = !1, Th = null, Eh = null, Dh = null, Oh = /* @__PURE__ */ new Map(), kh = /* @__PURE__ */ new Map(), Ah = [], jh = "mousedown mouseup touchcancel touchend touchstart auxclick dblclick pointercancel pointerdown pointerup dragend dragstart drop compositionend compositionstart keydown keypress keyup input textInput copy cut paste click change contextmenu reset".split(" ");
	function Mh(e, t) {
		switch (e) {
			case "focusin":
			case "focusout":
				Th = null;
				break;
			case "dragenter":
			case "dragleave":
				Eh = null;
				break;
			case "mouseover":
			case "mouseout":
				Dh = null;
				break;
			case "pointerover":
			case "pointerout":
				Oh.delete(t.pointerId);
				break;
			case "gotpointercapture":
			case "lostpointercapture": kh.delete(t.pointerId);
		}
	}
	function Nh(e, t, n, r, i, a) {
		return e === null || e.nativeEvent !== a ? (e = {
			blockedOn: t,
			domEventName: n,
			eventSystemFlags: r,
			nativeEvent: a,
			targetContainers: [i]
		}, t !== null && (t = Ft(t), t !== null && mh(t)), e) : (e.eventSystemFlags |= r, t = e.targetContainers, i !== null && t.indexOf(i) === -1 && t.push(i), e);
	}
	function Ph(e, t, n, r, i) {
		switch (t) {
			case "focusin": return Th = Nh(Th, e, t, n, r, i), !0;
			case "dragenter": return Eh = Nh(Eh, e, t, n, r, i), !0;
			case "mouseover": return Dh = Nh(Dh, e, t, n, r, i), !0;
			case "pointerover":
				var a = i.pointerId;
				return Oh.set(a, Nh(Oh.get(a) || null, e, t, n, r, i)), !0;
			case "gotpointercapture": return a = i.pointerId, kh.set(a, Nh(kh.get(a) || null, e, t, n, r, i)), !0;
		}
		return !1;
	}
	function Fh(e) {
		var t = Pt(e.target);
		if (t !== null) {
			var n = o(t);
			if (n !== null) {
				if (t = n.tag, t === 13) {
					if (t = s(n), t !== null) {
						e.blockedOn = t, Ct(e.priority, function() {
							hh(n);
						});
						return;
					}
				} else if (t === 31) {
					if (t = c(n), t !== null) {
						e.blockedOn = t, Ct(e.priority, function() {
							hh(n);
						});
						return;
					}
				} else if (t === 3 && n.stateNode.current.memoizedState.isDehydrated) {
					e.blockedOn = n.tag === 3 ? n.stateNode.containerInfo : null;
					return;
				}
			}
		}
		e.blockedOn = null;
	}
	function Ih(e) {
		if (e.blockedOn !== null) return !1;
		for (var t = e.targetContainers; 0 < t.length;) {
			var n = bh(e.nativeEvent);
			if (n === null) {
				n = e.nativeEvent;
				var r = new n.constructor(n.type, n);
				xn = r, n.target.dispatchEvent(r), xn = null;
			} else return t = Ft(n), t !== null && mh(t), e.blockedOn = n, !1;
			t.shift();
		}
		return !0;
	}
	function Lh(e, t, n) {
		Ih(e) && n.delete(t);
	}
	function Rh() {
		wh = !1, Th !== null && Ih(Th) && (Th = null), Eh !== null && Ih(Eh) && (Eh = null), Dh !== null && Ih(Dh) && (Dh = null), Oh.forEach(Lh), kh.forEach(Lh);
	}
	function zh(e, n) {
		e.blockedOn === n && (e.blockedOn = null, wh || (wh = !0, t.unstable_scheduleCallback(t.unstable_NormalPriority, Rh)));
	}
	var Bh = null;
	function Vh(e) {
		Bh !== e && (Bh = e, t.unstable_scheduleCallback(t.unstable_NormalPriority, function() {
			Bh === e && (Bh = null);
			for (var t = 0; t < e.length; t += 3) {
				var n = e[t], r = e[t + 1], i = e[t + 2];
				if (typeof r != "function") {
					if (Sh(r || n) === null) continue;
					break;
				}
				var a = Ft(n);
				a !== null && (e.splice(t, 3), t -= 3, nc(a, {
					pending: !0,
					data: i,
					method: n.method,
					action: r
				}, r, i));
			}
		}));
	}
	function Hh(e) {
		function t(t) {
			return zh(t, e);
		}
		Th !== null && zh(Th, e), Eh !== null && zh(Eh, e), Dh !== null && zh(Dh, e), Oh.forEach(t), kh.forEach(t);
		for (var n = 0; n < Ah.length; n++) {
			var r = Ah[n];
			r.blockedOn === e && (r.blockedOn = null);
		}
		for (; 0 < Ah.length && (n = Ah[0], n.blockedOn === null);) Fh(n), n.blockedOn === null && Ah.shift();
		if (n = (e.ownerDocument || e).$$reactFormReplay, n != null) for (r = 0; r < n.length; r += 3) {
			var i = n[r], a = n[r + 1], o = i[Tt] || null;
			if (typeof a == "function") o || Vh(n);
			else if (o) {
				var s = null;
				if (a && a.hasAttribute("formAction")) {
					if (i = a, o = a[Tt] || null) s = o.formAction;
					else if (Sh(i) !== null) continue;
				} else s = o.action;
				typeof s == "function" ? n[r + 1] = s : (n.splice(r, 3), r -= 3), Vh(n);
			}
		}
	}
	function Uh() {
		function e(e) {
			e.canIntercept && e.info === "react-transition" && e.intercept({
				handler: function() {
					return new Promise(function(e) {
						return i = e;
					});
				},
				focusReset: "manual",
				scroll: "manual"
			});
		}
		function t() {
			i !== null && (i(), i = null), r || setTimeout(n, 20);
		}
		function n() {
			if (!r && !navigation.transition) {
				var e = navigation.currentEntry;
				e && e.url != null && navigation.navigate(e.url, {
					state: e.getState(),
					info: "react-transition",
					history: "replace"
				});
			}
		}
		if (typeof navigation == "object") {
			var r = !1, i = null;
			return navigation.addEventListener("navigate", e), navigation.addEventListener("navigatesuccess", t), navigation.addEventListener("navigateerror", t), setTimeout(n, 100), function() {
				r = !0, navigation.removeEventListener("navigate", e), navigation.removeEventListener("navigatesuccess", t), navigation.removeEventListener("navigateerror", t), i !== null && (i(), i = null);
			};
		}
	}
	function Wh(e) {
		this._internalRoot = e;
	}
	Gh.prototype.render = Wh.prototype.render = function(e) {
		var t = this._internalRoot;
		if (t === null) throw Error(i(409));
		var n = t.current;
		dh(n, jd(), e, t, null, null);
	}, Gh.prototype.unmount = Wh.prototype.unmount = function() {
		var e = this._internalRoot;
		if (e !== null) {
			this._internalRoot = null;
			var t = e.containerInfo;
			dh(e.current, 2, null, e, null, null), zd(), t[Et] = null;
		}
	};
	function Gh(e) {
		this._internalRoot = e;
	}
	Gh.prototype.unstable_scheduleHydration = function(e) {
		if (e) {
			var t = St();
			e = {
				blockedOn: null,
				target: e,
				priority: t
			};
			for (var n = 0; n < Ah.length && t !== 0 && t < Ah[n].priority; n++);
			Ah.splice(n, 0, e), n === 0 && Fh(e);
		}
	};
	var Kh = n.version;
	if (Kh !== "19.3.0") throw Error(i(527, Kh, "19.3.0"));
	P.findDOMNode = function(e) {
		var t = e._reactInternals;
		if (t === void 0) throw typeof e.render == "function" ? Error(i(188)) : (e = Object.keys(e).join(","), Error(i(268, e)));
		return e = d(t), e = e === null ? null : p(e), e = e === null ? null : e.stateNode, e;
	};
	var qh = {
		bundleType: 0,
		version: "19.3.0",
		rendererPackageName: "react-dom",
		currentDispatcherRef: N,
		reconcilerVersion: "19.3.0"
	};
	if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u") {
		var Jh = __REACT_DEVTOOLS_GLOBAL_HOOK__;
		if (!Jh.isDisabled && Jh.supportsFiber) try {
			Qe = Jh.inject(qh), $e = Jh;
		} catch {}
	}
	e.createRoot = function(e, t) {
		if (!a(e)) throw Error(i(299));
		var n = !1, r = "", o = Tc, s = Ec, c = Dc;
		return t != null && (!0 === t.unstable_strictMode && (n = !0), t.identifierPrefix !== void 0 && (r = t.identifierPrefix), t.onUncaughtError !== void 0 && (o = t.onUncaughtError), t.onCaughtError !== void 0 && (s = t.onCaughtError), t.onRecoverableError !== void 0 && (c = t.onRecoverableError)), t = lh(e, 1, !1, null, null, n, r, null, o, s, c, Uh), e[Et] = t.current, Wf(e), new Wh(t);
	};
})), g = /* @__PURE__ */ o(((e, t) => {
	function n() {
		if (typeof __REACT_DEVTOOLS_GLOBAL_HOOK__ < "u" && typeof __REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE == "function") try {
			__REACT_DEVTOOLS_GLOBAL_HOOK__.checkDCE(n);
		} catch (e) {
			console.error(e);
		}
	}
	n(), t.exports = h();
})), _ = /* @__PURE__ */ c(u(), 1), v = g(), y = (e, t = document) => t.querySelector(e), b = (e) => new Intl.NumberFormat("ar-EG", { maximumFractionDigits: 2 }).format(e), x = (e) => e < 10 ? "0" + e : String(e), S = (e) => `${x(Math.floor(Math.max(0, e) / 60))}:${x(Math.floor(Math.max(0, e) % 60))}`, C = {
	draft: "مسودة",
	waiting: "قاعة الانتظار مفتوحة",
	running: "الامتحان جارٍ",
	closed: "انتهى الامتحان"
}, w;
function T(e, t = !1) {
	let n = y("#toast");
	if (!n.hidden && n.dataset.message === e) return;
	n.dataset.message = e;
	let r = document.createElement("span");
	r.textContent = e;
	let i = document.createElement("button");
	i.type = "button", i.className = "toast-close", i.setAttribute("aria-label", "إغلاق الرسالة"), i.textContent = "×", i.onclick = () => {
		clearTimeout(w), n.hidden = !0;
	}, n.textContent = "", n.appendChild(r), n.appendChild(i), n.className = `toast${t ? " error" : ""}`, n.hidden = !1, clearTimeout(w), w = setTimeout(() => {
		n.hidden = !0;
	}, 6e3);
}
function E(e, t, n, r = {}) {
	return new Promise((i, a) => {
		let o = new XMLHttpRequest();
		o.open(t === void 0 ? "GET" : "POST", e, !0), o.withCredentials = !0, o.timeout = r.timeoutMs || 12e3, o.setRequestHeader("X-Exam-Request", "1"), n && o.setRequestHeader("X-Admin-Token", n), t !== void 0 && o.setRequestHeader("Content-Type", "application/json"), o.onerror = () => a(/* @__PURE__ */ Error("تعذر الوصول لجهاز الامتحان. ابقَ على شبكة الامتحان وحاول مرة أخرى.")), o.ontimeout = () => a(/* @__PURE__ */ Error("انتهت مهلة الاتصال بجهاز الامتحان. حاول مرة أخرى.")), o.onabort = () => a(/* @__PURE__ */ Error("انقطع طلب الاتصال قبل تأكيد الاستلام.")), o.onload = () => {
			let e;
			try {
				e = JSON.parse(o.responseText);
			} catch {
				let e = /* @__PURE__ */ Error("وصل رد غير مكتمل من جهاز الامتحان. حاول مرة أخرى.");
				o.status >= 400 && (e.status = o.status), a(e);
				return;
			}
			if (o.status < 200 || o.status >= 300) {
				let t = Error(e.error || "تعذر إكمال العملية");
				t.status = o.status, a(t);
				return;
			}
			i(e);
		}, o.send(t === void 0 ? null : JSON.stringify(t));
	});
}
function D(e, t) {
	return typeof e.showModal == "function" ? (e.returnValue = "cancel", e.showModal(), new Promise((t) => e.addEventListener("close", () => t(e.returnValue === "yes"), { once: !0 }))) : Promise.resolve(window.confirm(t));
}
async function O(e) {
	if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(e);
	else {
		let t = document.createElement("textarea");
		t.value = e, document.body.append(t), t.select();
		let n = document.execCommand("copy");
		if (t.remove(), !n) throw Error("انسخ الرابط يدويًا من الحقل");
	}
	T("تم النسخ");
}
function k(e, t) {
	let n = URL.createObjectURL(e), r = document.createElement("a");
	r.href = n, r.download = t, document.body.appendChild(r), r.click(), r.remove(), setTimeout(() => URL.revokeObjectURL(n), 3e4);
}
async function ee(e, t) {
	let n = new Uint8Array(await e.slice(0, 16).arrayBuffer()), r = String.fromCharCode(...n);
	if (!e.size) throw Error("الملف فارغ؛ لم يتم حفظه. أعد التنزيل.");
	let i;
	if (t === "application/pdf") i = r.indexOf("%PDF-") === 0;
	else if (t === "application/octet-stream") i = r === "SQLite format 3\0";
	else if (t === "text/csv") i = !/^[\s\ufeff]*[<{\[]/.test(await e.slice(0, 256).text());
	else {
		let t = new Uint8Array(await e.slice(-65557).arrayBuffer());
		i = n[0] === 80 && n[1] === 75 && t.some((e, n) => e === 80 && t[n + 1] === 75 && t[n + 2] === 5 && t[n + 3] === 6 && n + 22 <= t.length && n + 22 + t[n + 20] + t[n + 21] * 256 === t.length);
	}
	if (!i) throw Error("الملف غير مكتمل أو غير صالح؛ لم يتم حفظه. أعد التنزيل من البرنامج.");
}
async function A(e, t, n) {
	let r = t.replace(/[\\/:*?"<>|]/g, "-"), i = window.pywebview?.api?.download_exam_file;
	if (i) return (await i(e, r, n)).saved;
	let { token: a } = await E("/api/bootstrap"), o = typeof AbortController == "function" ? new AbortController() : null, s = o ? setTimeout(() => o.abort(), 3e5) : null;
	try {
		let t = {
			"X-Exam-Request": "1",
			"X-Admin-Token": a
		}, i = e === "/api/backup";
		i && (t["Content-Type"] = "application/json");
		let s = await fetch(e, {
			headers: t,
			credentials: "same-origin",
			cache: "no-store",
			...i ? {
				method: "POST",
				body: "{}"
			} : {},
			...o ? { signal: o.signal } : {}
		});
		if (!s.ok) {
			let e = await s.json().catch(() => ({}));
			throw Error(e.error || "لم يكتمل تنزيل الملف؛ حاول مرة أخرى.");
		}
		if (s.headers.get("Content-Type")?.split(";")[0] !== n) throw Error("الاستجابة ليست الملف المطلوب؛ لم يتم حفظ ملف غير صالح.");
		let c = await s.blob();
		return await ee(c, n), k(c, r), !0;
	} finally {
		s && clearTimeout(s);
	}
}
function te(e, t) {
	let n = y("#confirm-dialog");
	return y("#confirm-title").textContent = e, y("#confirm-copy").textContent = t, D(n, `${e}\n${t}`);
}
//#endregion
//#region ../web/assets/qr.js
var ne;
((e) => {
	let t = class t {
		constructor(e, n, r, a) {
			if (this.version = e, this.errorCorrectionLevel = n, this.modules = [], this.isFunction = [], e < t.MIN_VERSION || e > t.MAX_VERSION) throw RangeError("Version value out of range");
			if (a < -1 || a > 7) throw RangeError("Mask value out of range");
			this.size = e * 4 + 17;
			let o = [];
			for (let e = 0; e < this.size; e++) o.push(!1);
			for (let e = 0; e < this.size; e++) this.modules.push(o.slice()), this.isFunction.push(o.slice());
			this.drawFunctionPatterns();
			let s = this.addEccAndInterleave(r);
			if (this.drawCodewords(s), a == -1) {
				let e = 1e9;
				for (let t = 0; t < 8; t++) {
					this.applyMask(t), this.drawFormatBits(t);
					let n = this.getPenaltyScore();
					n < e && (a = t, e = n), this.applyMask(t);
				}
			}
			i(0 <= a && a <= 7), this.mask = a, this.applyMask(a), this.drawFormatBits(a), this.isFunction = [];
		}
		static encodeText(n, r) {
			let i = e.QrSegment.makeSegments(n);
			return t.encodeSegments(i, r);
		}
		static encodeBinary(n, r) {
			let i = e.QrSegment.makeBytes(n);
			return t.encodeSegments([i], r);
		}
		static encodeSegments(e, r, a = 1, s = 40, c = -1, l = !0) {
			if (!(t.MIN_VERSION <= a && a <= s && s <= t.MAX_VERSION) || c < -1 || c > 7) throw RangeError("Invalid value");
			let u, d;
			for (u = a;; u++) {
				let n = t.getNumDataCodewords(u, r) * 8, i = o.getTotalBits(e, u);
				if (i <= n) {
					d = i;
					break;
				}
				if (u >= s) throw RangeError("Data too long");
			}
			for (let e of [
				t.Ecc.MEDIUM,
				t.Ecc.QUARTILE,
				t.Ecc.HIGH
			]) l && d <= t.getNumDataCodewords(u, e) * 8 && (r = e);
			let f = [];
			for (let t of e) {
				n(t.mode.modeBits, 4, f), n(t.numChars, t.mode.numCharCountBits(u), f);
				for (let e of t.getData()) f.push(e);
			}
			i(f.length == d);
			let p = t.getNumDataCodewords(u, r) * 8;
			i(f.length <= p), n(0, Math.min(4, p - f.length), f), n(0, (8 - f.length % 8) % 8, f), i(f.length % 8 == 0);
			for (let e = 236; f.length < p; e ^= 253) n(e, 8, f);
			let m = [];
			for (; m.length * 8 < f.length;) m.push(0);
			return f.forEach((e, t) => m[t >>> 3] |= e << 7 - (t & 7)), new t(u, r, m, c);
		}
		getModule(e, t) {
			return 0 <= e && e < this.size && 0 <= t && t < this.size && this.modules[t][e];
		}
		getModules() {
			return this.modules;
		}
		drawFunctionPatterns() {
			for (let e = 0; e < this.size; e++) this.setFunctionModule(6, e, e % 2 == 0), this.setFunctionModule(e, 6, e % 2 == 0);
			this.drawFinderPattern(3, 3), this.drawFinderPattern(this.size - 4, 3), this.drawFinderPattern(3, this.size - 4);
			let e = this.getAlignmentPatternPositions(), t = e.length;
			for (let n = 0; n < t; n++) for (let r = 0; r < t; r++) n == 0 && r == 0 || n == 0 && r == t - 1 || n == t - 1 && r == 0 || this.drawAlignmentPattern(e[n], e[r]);
			this.drawFormatBits(0), this.drawVersion();
		}
		drawFormatBits(e) {
			let t = this.errorCorrectionLevel.formatBits << 3 | e, n = t;
			for (let e = 0; e < 10; e++) n = n << 1 ^ (n >>> 9) * 1335;
			let a = (t << 10 | n) ^ 21522;
			i(!(a >>> 15));
			for (let e = 0; e <= 5; e++) this.setFunctionModule(8, e, r(a, e));
			this.setFunctionModule(8, 7, r(a, 6)), this.setFunctionModule(8, 8, r(a, 7)), this.setFunctionModule(7, 8, r(a, 8));
			for (let e = 9; e < 15; e++) this.setFunctionModule(14 - e, 8, r(a, e));
			for (let e = 0; e < 8; e++) this.setFunctionModule(this.size - 1 - e, 8, r(a, e));
			for (let e = 8; e < 15; e++) this.setFunctionModule(8, this.size - 15 + e, r(a, e));
			this.setFunctionModule(8, this.size - 8, !0);
		}
		drawVersion() {
			if (this.version < 7) return;
			let e = this.version;
			for (let t = 0; t < 12; t++) e = e << 1 ^ (e >>> 11) * 7973;
			let t = this.version << 12 | e;
			i(!(t >>> 18));
			for (let e = 0; e < 18; e++) {
				let n = r(t, e), i = this.size - 11 + e % 3, a = Math.floor(e / 3);
				this.setFunctionModule(i, a, n), this.setFunctionModule(a, i, n);
			}
		}
		drawFinderPattern(e, t) {
			for (let n = -4; n <= 4; n++) for (let r = -4; r <= 4; r++) {
				let i = Math.max(Math.abs(r), Math.abs(n)), a = e + r, o = t + n;
				0 <= a && a < this.size && 0 <= o && o < this.size && this.setFunctionModule(a, o, i != 2 && i != 4);
			}
		}
		drawAlignmentPattern(e, t) {
			for (let n = -2; n <= 2; n++) for (let r = -2; r <= 2; r++) this.setFunctionModule(e + r, t + n, Math.max(Math.abs(r), Math.abs(n)) != 1);
		}
		setFunctionModule(e, t, n) {
			this.modules[t][e] = n, this.isFunction[t][e] = !0;
		}
		addEccAndInterleave(e) {
			let n = this.version, r = this.errorCorrectionLevel;
			if (e.length != t.getNumDataCodewords(n, r)) throw RangeError("Invalid argument");
			let a = t.NUM_ERROR_CORRECTION_BLOCKS[r.ordinal][n], o = t.ECC_CODEWORDS_PER_BLOCK[r.ordinal][n], s = Math.floor(t.getNumRawDataModules(n) / 8), c = a - s % a, l = Math.floor(s / a), u = [], d = t.reedSolomonComputeDivisor(o);
			for (let n = 0, r = 0; n < a; n++) {
				let i = e.slice(r, r + l - o + (n < c ? 0 : 1));
				r += i.length;
				let a = t.reedSolomonComputeRemainder(i, d);
				n < c && i.push(0), u.push(i.concat(a));
			}
			let f = [];
			for (let e = 0; e < u[0].length; e++) u.forEach((t, n) => {
				(e != l - o || n >= c) && f.push(t[e]);
			});
			return i(f.length == s), f;
		}
		drawCodewords(e) {
			if (e.length != Math.floor(t.getNumRawDataModules(this.version) / 8)) throw RangeError("Invalid argument");
			let n = 0;
			for (let t = this.size - 1; t >= 1; t -= 2) {
				t == 6 && (t = 5);
				for (let i = 0; i < this.size; i++) for (let a = 0; a < 2; a++) {
					let o = t - a, s = t + 1 & 2 ? i : this.size - 1 - i;
					!this.isFunction[s][o] && n < e.length * 8 && (this.modules[s][o] = r(e[n >>> 3], 7 - (n & 7)), n++);
				}
			}
			i(n == e.length * 8);
		}
		applyMask(e) {
			if (e < 0 || e > 7) throw RangeError("Mask value out of range");
			for (let t = 0; t < this.size; t++) for (let n = 0; n < this.size; n++) {
				let r;
				switch (e) {
					case 0:
						r = (n + t) % 2 == 0;
						break;
					case 1:
						r = t % 2 == 0;
						break;
					case 2:
						r = n % 3 == 0;
						break;
					case 3:
						r = (n + t) % 3 == 0;
						break;
					case 4:
						r = (Math.floor(n / 3) + Math.floor(t / 2)) % 2 == 0;
						break;
					case 5:
						r = n * t % 2 + n * t % 3 == 0;
						break;
					case 6:
						r = (n * t % 2 + n * t % 3) % 2 == 0;
						break;
					case 7:
						r = ((n + t) % 2 + n * t % 3) % 2 == 0;
						break;
					default: throw Error("Unreachable");
				}
				!this.isFunction[t][n] && r && (this.modules[t][n] = !this.modules[t][n]);
			}
		}
		getPenaltyScore() {
			let e = 0;
			for (let n = 0; n < this.size; n++) {
				let r = !1, i = 0, a = [
					0,
					0,
					0,
					0,
					0,
					0,
					0
				];
				for (let o = 0; o < this.size; o++) this.modules[n][o] == r ? (i++, i == 5 ? e += t.PENALTY_N1 : i > 5 && e++) : (this.finderPenaltyAddHistory(i, a), r || (e += this.finderPenaltyCountPatterns(a) * t.PENALTY_N3), r = this.modules[n][o], i = 1);
				e += this.finderPenaltyTerminateAndCount(r, i, a) * t.PENALTY_N3;
			}
			for (let n = 0; n < this.size; n++) {
				let r = !1, i = 0, a = [
					0,
					0,
					0,
					0,
					0,
					0,
					0
				];
				for (let o = 0; o < this.size; o++) this.modules[o][n] == r ? (i++, i == 5 ? e += t.PENALTY_N1 : i > 5 && e++) : (this.finderPenaltyAddHistory(i, a), r || (e += this.finderPenaltyCountPatterns(a) * t.PENALTY_N3), r = this.modules[o][n], i = 1);
				e += this.finderPenaltyTerminateAndCount(r, i, a) * t.PENALTY_N3;
			}
			for (let n = 0; n < this.size - 1; n++) for (let r = 0; r < this.size - 1; r++) {
				let i = this.modules[n][r];
				i == this.modules[n][r + 1] && i == this.modules[n + 1][r] && i == this.modules[n + 1][r + 1] && (e += t.PENALTY_N2);
			}
			let n = 0;
			for (let e of this.modules) n = e.reduce((e, t) => e + +!!t, n);
			let r = this.size * this.size, a = Math.ceil(Math.abs(n * 20 - r * 10) / r) - 1;
			return i(0 <= a && a <= 9), e += a * t.PENALTY_N4, i(0 <= e && e <= 2568888), e;
		}
		getAlignmentPatternPositions() {
			if (this.version == 1) return [];
			{
				let e = Math.floor(this.version / 7) + 2, t = this.version == 32 ? 26 : Math.ceil((this.version * 4 + 4) / (e * 2 - 2)) * 2, n = [6];
				for (let r = this.size - 7; n.length < e; r -= t) n.splice(1, 0, r);
				return n;
			}
		}
		static getNumRawDataModules(e) {
			if (e < t.MIN_VERSION || e > t.MAX_VERSION) throw RangeError("Version number out of range");
			let n = (16 * e + 128) * e + 64;
			if (e >= 2) {
				let t = Math.floor(e / 7) + 2;
				n -= (25 * t - 10) * t - 55, e >= 7 && (n -= 36);
			}
			return i(208 <= n && n <= 29648), n;
		}
		static getNumDataCodewords(e, n) {
			return Math.floor(t.getNumRawDataModules(e) / 8) - t.ECC_CODEWORDS_PER_BLOCK[n.ordinal][e] * t.NUM_ERROR_CORRECTION_BLOCKS[n.ordinal][e];
		}
		static reedSolomonComputeDivisor(e) {
			if (e < 1 || e > 255) throw RangeError("Degree out of range");
			let n = [];
			for (let t = 0; t < e - 1; t++) n.push(0);
			n.push(1);
			let r = 1;
			for (let i = 0; i < e; i++) {
				for (let e = 0; e < n.length; e++) n[e] = t.reedSolomonMultiply(n[e], r), e + 1 < n.length && (n[e] ^= n[e + 1]);
				r = t.reedSolomonMultiply(r, 2);
			}
			return n;
		}
		static reedSolomonComputeRemainder(e, n) {
			let r = n.map((e) => 0);
			for (let i of e) {
				let e = i ^ r.shift();
				r.push(0), n.forEach((n, i) => r[i] ^= t.reedSolomonMultiply(n, e));
			}
			return r;
		}
		static reedSolomonMultiply(e, t) {
			if (e >>> 8 || t >>> 8) throw RangeError("Byte out of range");
			let n = 0;
			for (let r = 7; r >= 0; r--) n = n << 1 ^ (n >>> 7) * 285, n ^= (t >>> r & 1) * e;
			return i(!(n >>> 8)), n;
		}
		finderPenaltyCountPatterns(e) {
			let t = e[1];
			i(t <= this.size * 3);
			let n = t > 0 && e[2] == t && e[3] == t * 3 && e[4] == t && e[5] == t;
			return (n && e[0] >= t * 4 && e[6] >= t ? 1 : 0) + (n && e[6] >= t * 4 && e[0] >= t ? 1 : 0);
		}
		finderPenaltyTerminateAndCount(e, t, n) {
			return e && (this.finderPenaltyAddHistory(t, n), t = 0), t += this.size, this.finderPenaltyAddHistory(t, n), this.finderPenaltyCountPatterns(n);
		}
		finderPenaltyAddHistory(e, t) {
			t[0] == 0 && (e += this.size), t.pop(), t.unshift(e);
		}
	};
	t.MIN_VERSION = 1, t.MAX_VERSION = 40, t.PENALTY_N1 = 3, t.PENALTY_N2 = 3, t.PENALTY_N3 = 40, t.PENALTY_N4 = 10, t.ECC_CODEWORDS_PER_BLOCK = [
		[
			-1,
			7,
			10,
			15,
			20,
			26,
			18,
			20,
			24,
			30,
			18,
			20,
			24,
			26,
			30,
			22,
			24,
			28,
			30,
			28,
			28,
			28,
			28,
			30,
			30,
			26,
			28,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30
		],
		[
			-1,
			10,
			16,
			26,
			18,
			24,
			16,
			18,
			22,
			22,
			26,
			30,
			22,
			22,
			24,
			24,
			28,
			28,
			26,
			26,
			26,
			26,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28,
			28
		],
		[
			-1,
			13,
			22,
			18,
			26,
			18,
			24,
			18,
			22,
			20,
			24,
			28,
			26,
			24,
			20,
			30,
			24,
			28,
			28,
			26,
			30,
			28,
			30,
			30,
			30,
			30,
			28,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30
		],
		[
			-1,
			17,
			28,
			22,
			16,
			22,
			28,
			26,
			26,
			24,
			28,
			24,
			28,
			22,
			24,
			24,
			30,
			28,
			28,
			26,
			28,
			30,
			24,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30,
			30
		]
	], t.NUM_ERROR_CORRECTION_BLOCKS = [
		[
			-1,
			1,
			1,
			1,
			1,
			1,
			2,
			2,
			2,
			2,
			4,
			4,
			4,
			4,
			4,
			6,
			6,
			6,
			6,
			7,
			8,
			8,
			9,
			9,
			10,
			12,
			12,
			12,
			13,
			14,
			15,
			16,
			17,
			18,
			19,
			19,
			20,
			21,
			22,
			24,
			25
		],
		[
			-1,
			1,
			1,
			1,
			2,
			2,
			4,
			4,
			4,
			5,
			5,
			5,
			8,
			9,
			9,
			10,
			10,
			11,
			13,
			14,
			16,
			17,
			17,
			18,
			20,
			21,
			23,
			25,
			26,
			28,
			29,
			31,
			33,
			35,
			37,
			38,
			40,
			43,
			45,
			47,
			49
		],
		[
			-1,
			1,
			1,
			2,
			2,
			4,
			4,
			6,
			6,
			8,
			8,
			8,
			10,
			12,
			16,
			12,
			17,
			16,
			18,
			21,
			20,
			23,
			23,
			25,
			27,
			29,
			34,
			34,
			35,
			38,
			40,
			43,
			45,
			48,
			51,
			53,
			56,
			59,
			62,
			65,
			68
		],
		[
			-1,
			1,
			1,
			2,
			4,
			4,
			4,
			5,
			6,
			8,
			8,
			11,
			11,
			16,
			16,
			18,
			16,
			19,
			21,
			25,
			25,
			25,
			34,
			30,
			32,
			35,
			37,
			40,
			42,
			45,
			48,
			51,
			54,
			57,
			60,
			63,
			66,
			70,
			74,
			77,
			81
		]
	], e.QrCode = t;
	function n(e, t, n) {
		if (t < 0 || t > 31 || e >>> t) throw RangeError("Value out of range");
		for (let r = t - 1; r >= 0; r--) n.push(e >>> r & 1);
	}
	function r(e, t) {
		return !!(e >>> t & 1);
	}
	function i(e) {
		if (!e) throw Error("Assertion error");
	}
	let a = class e {
		constructor(e, t, n) {
			if (this.mode = e, this.numChars = t, this.bitData = n, t < 0) throw RangeError("Invalid argument");
			this.bitData = n.slice();
		}
		static makeBytes(t) {
			let r = [];
			for (let e of t) n(e, 8, r);
			return new e(e.Mode.BYTE, t.length, r);
		}
		static makeNumeric(t) {
			if (!e.isNumeric(t)) throw RangeError("String contains non-numeric characters");
			let r = [];
			for (let e = 0; e < t.length;) {
				let i = Math.min(t.length - e, 3);
				n(parseInt(t.substring(e, e + i), 10), i * 3 + 1, r), e += i;
			}
			return new e(e.Mode.NUMERIC, t.length, r);
		}
		static makeAlphanumeric(t) {
			if (!e.isAlphanumeric(t)) throw RangeError("String contains unencodable characters in alphanumeric mode");
			let r = [], i = 0;
			for (; i + 2 <= t.length; i += 2) {
				let a = e.ALPHANUMERIC_CHARSET.indexOf(t.charAt(i)) * 45;
				a += e.ALPHANUMERIC_CHARSET.indexOf(t.charAt(i + 1)), n(a, 11, r);
			}
			return i < t.length && n(e.ALPHANUMERIC_CHARSET.indexOf(t.charAt(i)), 6, r), new e(e.Mode.ALPHANUMERIC, t.length, r);
		}
		static makeSegments(t) {
			return t == "" ? [] : e.isNumeric(t) ? [e.makeNumeric(t)] : e.isAlphanumeric(t) ? [e.makeAlphanumeric(t)] : [e.makeBytes(e.toUtf8ByteArray(t))];
		}
		static makeEci(t) {
			let r = [];
			if (t < 0) throw RangeError("ECI assignment value out of range");
			if (t < 128) n(t, 8, r);
			else if (t < 16384) n(2, 2, r), n(t, 14, r);
			else if (t < 1e6) n(6, 3, r), n(t, 21, r);
			else throw RangeError("ECI assignment value out of range");
			return new e(e.Mode.ECI, 0, r);
		}
		static isNumeric(t) {
			return e.NUMERIC_REGEX.test(t);
		}
		static isAlphanumeric(t) {
			return e.ALPHANUMERIC_REGEX.test(t);
		}
		getData() {
			return this.bitData.slice();
		}
		static getTotalBits(e, t) {
			let n = 0;
			for (let r of e) {
				let e = r.mode.numCharCountBits(t);
				if (r.numChars >= 1 << e) return Infinity;
				n += 4 + e + r.bitData.length;
			}
			return n;
		}
		static toUtf8ByteArray(e) {
			e = encodeURI(e);
			let t = [];
			for (let n = 0; n < e.length; n++) e.charAt(n) == "%" ? (t.push(parseInt(e.substring(n + 1, n + 3), 16)), n += 2) : t.push(e.charCodeAt(n));
			return t;
		}
	};
	a.NUMERIC_REGEX = /^[0-9]*$/, a.ALPHANUMERIC_REGEX = /^[A-Z0-9 $%*+.\/:-]*$/, a.ALPHANUMERIC_CHARSET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
	let o = a;
	e.QrSegment = a;
})(ne ||= {}), ((e) => {
	((e) => {
		let t = class {
			constructor(e, t) {
				this.ordinal = e, this.formatBits = t;
			}
		};
		t.LOW = new t(0, 1), t.MEDIUM = new t(1, 0), t.QUARTILE = new t(2, 3), t.HIGH = new t(3, 2), e.Ecc = t;
	})(e.QrCode ||= {});
})(ne ||= {}), ((e) => {
	((e) => {
		let t = class {
			constructor(e, t) {
				this.modeBits = e, this.numBitsCharCount = t;
			}
			numCharCountBits(e) {
				return this.numBitsCharCount[Math.floor((e + 7) / 17)];
			}
		};
		t.NUMERIC = new t(1, [
			10,
			12,
			14
		]), t.ALPHANUMERIC = new t(2, [
			9,
			11,
			13
		]), t.BYTE = new t(4, [
			8,
			16,
			16
		]), t.KANJI = new t(8, [
			8,
			10,
			12
		]), t.ECI = new t(7, [
			0,
			0,
			0
		]), e.Mode = t;
	})(e.QrSegment ||= {});
})(ne ||= {});
var re = ne.QrCode, j = /* @__PURE__ */ o(((e) => {
	var t = Symbol.for("react.transitional.element"), n = Symbol.for("react.fragment");
	function r(e, n, r) {
		var i = null;
		if (r !== void 0 && (i = "" + r), n.key !== void 0 && (i = "" + n.key), "key" in n) for (var a in r = {}, n) a !== "key" && (r[a] = n[a]);
		else r = n;
		return n = r.ref, {
			$$typeof: t,
			type: e,
			key: i,
			ref: n === void 0 ? null : n,
			props: r
		};
	}
	e.Fragment = n, e.jsx = r, e.jsxs = r;
})), M = (/* @__PURE__ */ o(((e, t) => {
	t.exports = j();
})))(), ie = (e) => ({
	kind: e,
	text: "",
	points: 2,
	options: [
		"",
		"",
		"",
		""
	],
	correct: 0,
	modelAnswer: ""
}), ae = {
	title: "",
	instructions: "",
	minutes: 15,
	timerMode: "shared",
	allowLate: !0,
	shuffleOptions: !0,
	shuffle: !1,
	questionCount: 1,
	mcqCount: 1,
	essayCount: 0,
	questions: [ie("mcq")]
}, oe = (e, t) => e.questions.filter((e) => e.kind === t).length, se = (e) => {
	let t = structuredClone(e || ae);
	return t.mcqCount == null && t.questionCount === t.questions.length && (t.mcqCount = oe(t, "mcq"), t.essayCount = oe(t, "essay")), t;
};
function ce({ config: e, onSave: t, onCancel: n, onDirty: r }) {
	let [i, a] = (0, _.useState)(() => se(e)), [o, s] = (0, _.useState)(!1), c = (e) => {
		a((t) => {
			let n = e(structuredClone(t));
			return n.mcqCount != null && (n.questionCount = n.mcqCount + n.essayCount), n;
		}), r(!0);
	}, l = i.mcqCount != null, u = (e) => c((t) => (e ? (t.mcqCount = oe(t, "mcq"), t.essayCount = oe(t, "essay")) : (delete t.mcqCount, delete t.essayCount), t)), d = (e, t) => c((n) => ({
		...n,
		[e]: t
	})), f = (e, t, n) => c((r) => (r.questions[e][t] = n, r)), p = (e, t, n) => c((r) => (r.questions[e].options[t] = n, r)), m = (e) => {
		if (i.questions.length >= 100) {
			T("الحد الأقصى ١٠٠ سؤال", !0);
			return;
		}
		c((t) => {
			let n = l ? t[e + "Count"] === oe(t, e) : t.questionCount === t.questions.length;
			return t.questions.push(ie(e)), n && (l ? t[e + "Count"]++ : t.questionCount++), t;
		});
	}, h = (e) => c((t) => {
		let n = t.questions[e].kind;
		return t.questions.splice(e, 1), l ? t[n + "Count"] = Math.min(t[n + "Count"], oe(t, n)) : t.questionCount = Math.min(t.questionCount, t.questions.length), t;
	});
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsxs)("div", {
		className: "page-heading",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
			/* @__PURE__ */ (0, M.jsx)("span", {
				className: "context-label",
				children: "تجهيز الامتحان"
			}),
			/* @__PURE__ */ (0, M.jsx)("h1", { children: e ? "تعديل الامتحان" : "امتحان جديد" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: "سمِّ الامتحان وجهّز أسئلته هنا، ثم اختره مع الحصة في قاعة السنتر." })
		] }), /* @__PURE__ */ (0, M.jsx)("button", {
			className: "button secondary",
			onClick: n,
			children: "رجوع للمكتبة"
		})]
	}), /* @__PURE__ */ (0, M.jsxs)("form", {
		id: "exam-editor",
		onSubmit: async (e) => {
			if (e.preventDefault(), i.questionCount < 1) {
				T("اختر سؤالًا واحدًا على الأقل لكل طالب", !0);
				return;
			}
			s(!0);
			try {
				await t(i), r(!1);
			} catch (e) {
				T(e.message, !0);
			} finally {
				s(!1);
			}
		},
		children: [
			/* @__PURE__ */ (0, M.jsxs)("section", {
				className: "panel editor-basics",
				children: [
					/* @__PURE__ */ (0, M.jsx)("h2", { children: "تفاصيل الامتحان" }),
					/* @__PURE__ */ (0, M.jsxs)("label", { children: ["اسم الامتحان", /* @__PURE__ */ (0, M.jsx)("input", {
						name: "title",
						required: !0,
						maxLength: "150",
						value: i.title,
						onChange: (e) => d("title", e.target.value),
						placeholder: "مثلًا: مراجعة الفصل الأول"
					})] }),
					/* @__PURE__ */ (0, M.jsxs)("label", { children: [
						"تعليمات للطلاب ",
						/* @__PURE__ */ (0, M.jsx)("span", {
							className: "muted",
							children: "اختياري"
						}),
						/* @__PURE__ */ (0, M.jsx)("textarea", {
							name: "instructions",
							rows: "2",
							maxLength: "2000",
							value: i.instructions,
							onChange: (e) => d("instructions", e.target.value)
						})
					] }),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "form-grid",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("label", { children: ["المدة بالدقائق", /* @__PURE__ */ (0, M.jsx)("input", {
								name: "minutes",
								type: "number",
								min: "1",
								max: "240",
								required: !0,
								value: i.minutes,
								onChange: (e) => d("minutes", Number(e.target.value))
							})] }),
							!l && /* @__PURE__ */ (0, M.jsxs)("label", { children: ["عدد الأسئلة اللي تظهر لكل طالب", /* @__PURE__ */ (0, M.jsx)("input", {
								name: "questionCount",
								type: "number",
								min: "1",
								max: i.questions.length,
								required: !0,
								value: i.questionCount,
								onChange: (e) => d("questionCount", Number(e.target.value))
							})] }),
							/* @__PURE__ */ (0, M.jsxs)("label", { children: ["نظام الوقت", /* @__PURE__ */ (0, M.jsxs)("select", {
								name: "timerMode",
								value: i.timerMode,
								onChange: (e) => d("timerMode", e.target.value),
								children: [/* @__PURE__ */ (0, M.jsx)("option", {
									value: "shared",
									children: "نهاية موحدة للجميع"
								}), /* @__PURE__ */ (0, M.jsx)("option", {
									value: "individual",
									children: "مدة كاملة لكل طالب"
								})]
							})] })
						]
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "check-label",
						children: [/* @__PURE__ */ (0, M.jsx)("input", {
							type: "checkbox",
							checked: l,
							onChange: (e) => u(e.target.checked)
						}), "تحديد عدد الاختياري والمقالي كل نوع لوحده"]
					}),
					l && /* @__PURE__ */ (0, M.jsxs)("div", {
						className: "form-grid",
						children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: [
							"عدد الاختياري لكل طالب",
							/* @__PURE__ */ (0, M.jsx)("input", {
								name: "mcqCount",
								type: "number",
								min: "0",
								max: oe(i, "mcq"),
								required: !0,
								value: i.mcqCount,
								onChange: (e) => d("mcqCount", Number(e.target.value))
							}),
							/* @__PURE__ */ (0, M.jsxs)("span", {
								className: "help",
								children: [
									"متاح في البنك: ",
									b(oe(i, "mcq")),
									" سؤال اختياري"
								]
							})
						] }), /* @__PURE__ */ (0, M.jsxs)("label", { children: [
							"عدد المقالي لكل طالب",
							/* @__PURE__ */ (0, M.jsx)("input", {
								name: "essayCount",
								type: "number",
								min: "0",
								max: oe(i, "essay"),
								required: !0,
								value: i.essayCount,
								onChange: (e) => d("essayCount", Number(e.target.value))
							}),
							/* @__PURE__ */ (0, M.jsxs)("span", {
								className: "help",
								children: [
									"متاح في البنك: ",
									b(oe(i, "essay")),
									" سؤال مقالي"
								]
							})
						] })]
					}),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "question-bank-summary",
						"aria-live": "polite",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("strong", { children: [
								"بنك الأسئلة: ",
								b(i.questions.length),
								" سؤال · يظهر لكل طالب: ",
								b(i.questionCount),
								" سؤال"
							] }),
							l && /* @__PURE__ */ (0, M.jsxs)("p", { children: [
								"اختياري: ",
								b(i.mcqCount),
								" من ",
								b(oe(i, "mcq")),
								" · مقالي: ",
								b(i.essayCount),
								" من ",
								b(oe(i, "essay"))
							] }),
							/* @__PURE__ */ (0, M.jsxs)("p", { children: [l ? "أضف الأسئلة في البنك تحت، ثم حدد العدد المعروض من كل نوع؛ مثلًا ٥ اختياري من ٢٠، و١ مقالي من ٢. صفر يعني عدم عرض أسئلة من هذا النوع." : "تُسحب الأسئلة من البنك كله دون تحديد عدد لكل نوع.", " السحب عشوائي لكل طالب بدون تكرار السؤال داخل محاولته، وأسئلته تظل محفوظة عند الخروج والعودة."] })
						]
					}),
					/* @__PURE__ */ (0, M.jsxs)("p", {
						className: "help",
						children: ["في النهاية الموحدة، الطالب المتأخر يأخذ الوقت المتبقي. ", l ? "عند سحب جزء من نوع معين، لازم درجات أسئلة النوع ده تتساوى. ممكن درجة المقالي تختلف عن الاختياري." : "عند سحب جزء من البنك كله، لازم درجات الأسئلة كلها تتساوى."]
					}),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "check-row",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("label", {
								className: "check-label",
								children: [/* @__PURE__ */ (0, M.jsx)("input", {
									type: "checkbox",
									checked: i.allowLate,
									onChange: (e) => d("allowLate", e.target.checked)
								}), "السماح بالدخول بعد البداية"]
							}),
							/* @__PURE__ */ (0, M.jsxs)("label", {
								className: "check-label",
								children: [/* @__PURE__ */ (0, M.jsx)("input", {
									type: "checkbox",
									checked: i.shuffle,
									onChange: (e) => d("shuffle", e.target.checked)
								}), "تغيير ترتيب الأسئلة بين الطلاب"]
							}),
							/* @__PURE__ */ (0, M.jsxs)("label", {
								className: "check-label",
								children: [/* @__PURE__ */ (0, M.jsx)("input", {
									type: "checkbox",
									checked: i.shuffleOptions !== !1,
									onChange: (e) => d("shuffleOptions", e.target.checked)
								}), "تغيير ترتيب الاختيارات بين الطلاب"]
							})
						]
					})
				]
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top questions-heading",
				children: [/* @__PURE__ */ (0, M.jsxs)("h2", { children: ["بنك الأسئلة ", /* @__PURE__ */ (0, M.jsxs)("span", {
					className: "muted",
					children: [
						"(",
						b(i.questions.length),
						")"
					]
				})] }), /* @__PURE__ */ (0, M.jsx)("span", {
					className: "muted",
					children: "الإجابات النموذجية تظهر للإدارة فقط"
				})]
			}),
			/* @__PURE__ */ (0, M.jsx)("div", {
				id: "question-list",
				children: i.questions.map((e, t) => /* @__PURE__ */ (0, M.jsxs)("section", {
					className: "question-editor",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("div", {
							className: "section-top",
							children: [/* @__PURE__ */ (0, M.jsxs)("h3", { children: [
								"السؤال ",
								b(t + 1),
								" ",
								/* @__PURE__ */ (0, M.jsx)("span", {
									className: "badge quiet",
									children: e.kind === "mcq" ? "اختيار من متعدد" : "مقالي"
								})
							] }), /* @__PURE__ */ (0, M.jsx)("button", {
								type: "button",
								className: "text-button danger-text",
								disabled: i.questions.length === 1,
								onClick: () => h(t),
								children: "حذف السؤال"
							})]
						}),
						/* @__PURE__ */ (0, M.jsxs)("div", {
							className: "question-fields",
							children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["نص السؤال", /* @__PURE__ */ (0, M.jsx)("textarea", {
								required: !0,
								rows: "2",
								maxLength: "5000",
								value: e.text,
								onChange: (e) => f(t, "text", e.target.value),
								placeholder: "اكتب السؤال هنا…"
							})] }), /* @__PURE__ */ (0, M.jsxs)("label", { children: ["الدرجة", /* @__PURE__ */ (0, M.jsx)("input", {
								type: "number",
								min: "1",
								max: "100",
								required: !0,
								value: e.points,
								onChange: (e) => f(t, "points", Number(e.target.value))
							})] })]
						}),
						e.kind === "mcq" ? /* @__PURE__ */ (0, M.jsxs)("fieldset", {
							className: "options-editor",
							children: [/* @__PURE__ */ (0, M.jsx)("legend", { children: "الاختيارات · حدد الإجابة الصحيحة" }), e.options.map((n, r) => /* @__PURE__ */ (0, M.jsxs)("label", {
								className: "option-editor",
								children: [/* @__PURE__ */ (0, M.jsx)("input", {
									type: "radio",
									name: `correct-${t}`,
									checked: e.correct === r,
									onChange: () => f(t, "correct", r),
									"aria-label": `الاختيار الصحيح ${r + 1}`
								}), /* @__PURE__ */ (0, M.jsx)("input", {
									className: "option-text",
									value: n,
									maxLength: "1000",
									required: !0,
									onChange: (e) => p(t, r, e.target.value),
									"aria-label": `نص الاختيار ${r + 1}`,
									placeholder: `الاختيار ${b(r + 1)}`
								})]
							}, r))]
						}) : /* @__PURE__ */ (0, M.jsxs)("label", { children: ["الإجابة النموذجية ومعايير التصحيح", /* @__PURE__ */ (0, M.jsx)("textarea", {
							rows: "3",
							maxLength: "10000",
							required: !0,
							value: e.modelAnswer,
							onChange: (e) => f(t, "modelAnswer", e.target.value),
							placeholder: "اكتب النقاط التي تُمنح عليها الدرجة…"
						})] })
					]
				}, e.id || t))
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "add-questions",
				children: [/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					type: "button",
					onClick: () => m("mcq"),
					children: "＋ سؤال اختيارات"
				}), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					type: "button",
					onClick: () => m("essay"),
					children: "＋ سؤال مقالي"
				})]
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "editor-footer",
				children: [/* @__PURE__ */ (0, M.jsx)("p", { children: "الطلاب لن يروا هذا الامتحان حتى تختاره مع حصة وتفتح القاعة." }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button",
					type: "submit",
					disabled: o,
					children: o ? "جارٍ الحفظ…" : "حفظ الامتحان"
				})]
			})
		]
	})] });
}
//#endregion
//#region src/networkStatus.js
var le = null, ue = /* @__PURE__ */ new Set();
function de(e) {
	le = e, ue.forEach((e) => e());
}
function fe(e) {
	return ue.add(e), () => ue.delete(e);
}
function pe() {
	return (0, _.useSyncExternalStore)(fe, () => le);
}
function me(e) {
	let t = e?.capport;
	if (t?.appliedMode !== "https" || !["ready", "expiring"].includes(t?.state)) return "";
	let n = t.installed?.hostname;
	return typeof n != "string" || !/^([a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z][a-z0-9-]+$/i.test(n) ? "" : `https://${n.toLowerCase()}/`;
}
//#endregion
//#region src/ExamHealth.jsx
function he() {
	let [e, t] = (0, _.useState)(null), [n, r] = (0, _.useState)(!1);
	if ((0, _.useEffect)(() => {
		let e = !0, n, i = !1, a = async () => {
			let o = window.pywebview?.api;
			if (i || !o?.exam_health) return;
			i = !0, clearTimeout(n);
			let s = setTimeout(() => {
				e && (r(!0), de(null));
			}, 15e3);
			try {
				let n = await o.exam_health();
				e && (t(n), de(n), r(!1));
			} catch {
				e && (r(!0), de(null));
			} finally {
				clearTimeout(s), i = !1, e && (n = setTimeout(a, 1e4));
			}
		};
		return window.addEventListener("pywebviewready", a), a(), () => {
			e = !1, clearTimeout(n), window.removeEventListener("pywebviewready", a);
		};
	}, []), (!e || !e.supported) && !n) return null;
	let i = [];
	return n ? i.push("تعذر تأكيد حالة الشبكة؛ افتح الإعدادات وافحص الاتصال.") : e?.inspectionError ? i.push(e.message || "تعذر تأكيد حالة الشبكة؛ افتح الإعدادات وافحص الاتصال.") : e?.supported && (e.ethernetDetail ? i.push(e.ethernetDetail) : e.ethernetActive ? (e.ethernetAddressReady === !1 || e.ethernetIp !== "10.77.0.1") && i.push("عنوان شبكة الامتحان غير موجود على منفذ الهاب. افحص تجهيز الماك.") : i.push("كابل شبكة الامتحان غير متصل."), e.dhcpRunning ? (e.dhcpConfigurationReady === !1 || e.dhcpSocketReady !== !0) && e.dhcpDetail !== e.ethernetDetail && i.push(e.dhcpDetail || "تعذر التحقق من استقبال طلبات توزيع العناوين.") : i.push("خدمة توزيع عناوين الطلاب متوقفة."), e.portalRunning || i.push("خدمة صفحة الطلاب متوقفة."), e.ethernetActive && e.dhcpRunning && e.portalRunning && e.ethernetIp === "10.77.0.1" && !e.androidProbe?.ready && i.push(e.androidProbe?.detail || "فحص الدخول وخدمة الامتحان لم ينجح. افتح الإعدادات للتفاصيل.")), e?.capport?.appliedMode !== "legacy" && [
		"expired",
		"expiring",
		"error",
		"unreachable"
	].includes(e?.capport?.state) && i.push(e.capport.detail), ["warning", "invalid"].includes(e?.dhcpLeases?.status) && i.push(e.dhcpLeases.detail), e?.sleepProtection?.supported && !e.sleepProtection.active && i.push("حماية الماك من النوم غير مفعلة؛ اتركه مستيقظًا وراجع الإعدادات."), /* @__PURE__ */ (0, M.jsxs)("aside", {
		className: "notice",
		role: i.length ? "alert" : "status",
		"aria-label": "حالة جهاز الامتحان",
		children: [
			/* @__PURE__ */ (0, M.jsx)("strong", { children: i.length ? "تنبيه جهاز الامتحان" : "جهاز الامتحان جاهز" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: i.length ? i.join(" ") : "خدمات الشبكة المباشرة تعمل، وحماية النوم: " + (e?.sleepProtection?.active ? "مفعلة" : "غير متاحة") + "." }),
			i.length > 0 && /* @__PURE__ */ (0, M.jsx)("p", { children: "رجّع الاتصال قبل متابعة الامتحان، وراجع حالة حفظ الطلاب؛ إعادة تهيئة الشبكة توقف الاتصال مؤقتًا." })
		]
	});
}
//#endregion
//#region src/RecoverAnswers.jsx
function ge({ attempt: e, exam: t, request: n, refresh: r }) {
	let [i, a] = (0, _.useState)(null), [o, s] = (0, _.useState)(""), [c, l] = (0, _.useState)(!1);
	if (!e.submitted_at || e.cancelled_at) return null;
	let u = async (n) => {
		let r = n.target.files[0];
		if (a(null), r) try {
			if (r.size > 15e5) throw Error("ملف الاسترجاع أكبر من الحد المسموح.");
			let n = JSON.parse(await r.text());
			if (n.format !== "massar-answer-backup-v1" || n.attemptId !== e.id || n.examId !== t.id) throw Error("الملف لا يخص محاولة هذا الطالب في هذه الجلسة.");
			if (!n.answers || typeof n.answers != "object" || Array.isArray(n.answers)) throw Error("صيغة الإجابات غير صالحة.");
			let i = Object.keys(n.answers);
			if (i.some((t) => !e.questionIds.includes(t))) throw Error("النسخة تحتوي أسئلة خارج محاولة الطالب.");
			let o = i.filter((t) => n.answers[t] !== e.answers[t]);
			if (!o.length) throw Error("الإجابات الموجودة في الملف وصلت بالفعل؛ لا توجد فروق.");
			a({
				backup: n,
				changes: o,
				version: e.recoveryVersion
			});
		} catch (e) {
			T(e.message, !0);
		}
	}, d = (e, t) => t == null || t === "" ? "لم يجب" : e.kind === "mcq" ? e.options[t] || "اختيار غير صالح" : String(t);
	return /* @__PURE__ */ (0, M.jsxs)("details", {
		className: "panel",
		children: [
			/* @__PURE__ */ (0, M.jsx)("summary", { children: "استرجاع إجابات لم تصل من موبايل الطالب" }),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "اطلب من الطالب تنزيل «نسخة احتياطية للإجابات». الملف ليس دليلًا على وقت كتابة الإجابة؛ راجع الفروق قبل اعتماده. لا يتغير وقت التسليم الأصلي."
			}),
			/* @__PURE__ */ (0, M.jsxs)("label", { children: ["ملف الإجابات من الطالب", /* @__PURE__ */ (0, M.jsx)("input", {
				type: "file",
				accept: "application/json,.json",
				onChange: u,
				disabled: c
			})] }),
			i && /* @__PURE__ */ (0, M.jsxs)("form", {
				onSubmit: async (t) => {
					if (t.preventDefault(), await te("اعتماد الإجابات المسترجعة؟", "الملف قابل للتعديل، ووقت كتابته غير مُثبت. اعتمد الفروق فقط بعد التحقق مع الطالب. سيتحدث تصحيح الاختياري وتُعاد مراجعة المقالي المتغير.")) {
						l(!0);
						try {
							await n(`/api/attempts/${e.id}/recover-answers`, {
								...i.backup,
								expectedVersion: i.version,
								reason: o,
								confirmed: !0
							}), a(null), s(""), await r(), T("تم اعتماد الاسترجاع وحفظ الورقة السابقة في سجل المراجعة.");
						} catch (e) {
							T(e.message, !0);
						} finally {
							l(!1);
						}
					}
				},
				children: [
					i.changes.map((n) => {
						let r = t.config.questions.find((e) => e.id === n);
						return /* @__PURE__ */ (0, M.jsxs)("article", {
							className: "review-question",
							children: [
								/* @__PURE__ */ (0, M.jsx)("h3", { children: r.text }),
								/* @__PURE__ */ (0, M.jsxs)("p", { children: [
									/* @__PURE__ */ (0, M.jsx)("strong", { children: "المحفوظ على الماك:" }),
									" ",
									d(r, e.answers[n])
								] }),
								/* @__PURE__ */ (0, M.jsxs)("p", { children: [
									/* @__PURE__ */ (0, M.jsx)("strong", { children: "نسخة الطالب:" }),
									" ",
									d(r, i.backup.answers[n])
								] })
							]
						}, n);
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", { children: ["سبب اعتماد الاسترجاع", /* @__PURE__ */ (0, M.jsx)("textarea", {
						required: !0,
						minLength: "5",
						maxLength: "1000",
						value: o,
						onChange: (e) => s(e.target.value)
					})] }),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						disabled: c || i.version !== e.recoveryVersion,
						children: c ? "جارٍ الاعتماد…" : "راجعت الفروق وأعتمد الاسترجاع"
					}),
					i.version !== e.recoveryVersion && /* @__PURE__ */ (0, M.jsx)("p", {
						role: "alert",
						children: "تغيرت الورقة؛ اختر الملف مرة أخرى لمراجعة أحدث الفروق."
					})
				]
			})
		]
	});
}
//#endregion
//#region src/PortalConfiguration.jsx
var _e = {
	legacy: "دخول محلي بدون شهادة",
	https: "دخول آمن بشهادة"
};
function N({ status: e, disabled: t, refresh: n }) {
	let r = e?.capport, i = r?.desiredMode, a = r?.appliedMode, [o, s] = (0, _.useState)("exam.massar-academy.net"), [c, l] = (0, _.useState)(!1), [u, d] = (0, _.useState)(!1), [f, p] = (0, _.useState)(""), m = async (e) => {
		d(!0), p("");
		try {
			await window.pywebview.api.select_portal_mode(e), await n(), p("تم حفظ الاختيار. طبّقه بزر تحديث تجهيز الماك بعد إغلاق القاعات.");
		} catch (e) {
			p(e.message || "تعذر حفظ طريقة الدخول.");
		} finally {
			d(!1);
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("details", {
		className: "notice",
		children: [
			/* @__PURE__ */ (0, M.jsx)("summary", { children: "طريقة دخول الطلاب" }),
			/* @__PURE__ */ (0, M.jsxs)("p", { children: ["المطبّق حاليًا على الشبكة: ", /* @__PURE__ */ (0, M.jsx)("strong", { children: _e[a] || "لم يتم التأكد من الإعداد" })] }),
			/* @__PURE__ */ (0, M.jsxs)("fieldset", {
				disabled: t || c || u,
				children: [
					/* @__PURE__ */ (0, M.jsx)("legend", { children: "الطريقة المختارة للتجهيز" }),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "multiple-rooms-option",
						children: [/* @__PURE__ */ (0, M.jsx)("input", {
							type: "radio",
							name: "portal-mode",
							value: "legacy",
							checked: i === "legacy",
							onChange: () => m("legacy")
						}), /* @__PURE__ */ (0, M.jsxs)("span", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "دخول محلي بدون شهادة" }), /* @__PURE__ */ (0, M.jsx)("small", { children: "يعمل بعنوان 10.77.0.1 ويحاول إظهار إشعار الدخول بالطريقة التقليدية. لا يحتاج شهادة أو تجديدًا." })] })]
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "multiple-rooms-option",
						children: [/* @__PURE__ */ (0, M.jsx)("input", {
							type: "radio",
							name: "portal-mode",
							value: "https",
							checked: i === "https",
							onChange: () => m("https")
						}), /* @__PURE__ */ (0, M.jsxs)("span", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "دخول آمن بشهادة" }), /* @__PURE__ */ (0, M.jsx)("small", { children: "يضيف طريقة الاكتشاف المعيارية للأجهزة الداعمة، ويحتاج شهادة موثوقة صالحة وتجديدها قبل انتهائها." })] })]
					})
				]
			}),
			r?.pendingModeChange && /* @__PURE__ */ (0, M.jsx)("p", {
				role: "status",
				children: "الاختيار المحفوظ لم يُطبّق بعد. أغلق القاعات ثم اضغط «تحديث تجهيز الماك». حفظ الاختيار وحده لا يغيّر إعداد الشبكة أو رابط الدخول."
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				role: "status",
				children: r?.detail || "جارٍ قراءة طريقة دخول الطلاب."
			}),
			i === "https" && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
				r?.pendingCertificateChange && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "الشهادة المحفوظة تنتظر التطبيق بزر «تحديث تجهيز الماك» بعد إغلاق القاعات. البوابة الحالية تستمر بشهادتها المطبّقة."
				}),
				/* @__PURE__ */ (0, M.jsxs)("p", { children: [
					"اسم الموقع يوجّه الطلاب إلى الماك داخل واي فاي الامتحان. عنوان كابل الامتحان ثابت: ",
					/* @__PURE__ */ (0, M.jsx)("b", {
						dir: "ltr",
						children: "10.77.0.1"
					}),
					"."
				] }),
				/* @__PURE__ */ (0, M.jsx)("p", { children: "إصدار الشهادة وتجديدها يحتاجان إنترنت وإثبات ملكية الدومين. صفحة الامتحان تعمل محليًا. افحص ساعة الموبايلات وتاريخ انتهاء الشهادة؛ التجديد ليس تلقائيًا." }),
				r?.installed && a === "https" && /* @__PURE__ */ (0, M.jsxs)("p", { children: [
					"العنوان المُجهز: ",
					/* @__PURE__ */ (0, M.jsxs)("b", {
						dir: "ltr",
						children: [
							"https://",
							r.installed.hostname,
							"/"
						]
					}),
					/* @__PURE__ */ (0, M.jsx)("br", {}),
					"انتهاء الشهادة: ",
					new Date(r.installed.expiresAt).toLocaleDateString("ar-EG")
				] }),
				r?.candidate && /* @__PURE__ */ (0, M.jsxs)("p", { children: [
					"الشهادة المحفوظة: ",
					/* @__PURE__ */ (0, M.jsx)("b", {
						dir: "ltr",
						children: r.candidate.hostname
					}),
					" · تنتهي ",
					new Date(r.candidate.expiresAt).toLocaleDateString("ar-EG")
				] }),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["عنوان شبكة الامتحان", /* @__PURE__ */ (0, M.jsx)("input", {
					dir: "ltr",
					value: o,
					onChange: (e) => s(e.target.value),
					autoCapitalize: "none",
					spellCheck: !1,
					placeholder: "exam.massar-academy.net"
				})] }),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "اختار شهادة عامة موثوقة وكاملة fullchain.pem، ثم المفتاح المطابق privkey.pem. شهادة Cloudflare Origin وحدها لا تصلح للموبايلات."
				}),
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					disabled: t || c || u || !o.trim(),
					onClick: async () => {
						l(!0), p("اختار ملف fullchain.pem أولًا، وبعده ملف privkey.pem.");
						try {
							let e = await window.pywebview.api.select_portal_certificate(o);
							p(e.saved ? "تم فحص الشهادة وحفظها. طبّقها بزر تحديث تجهيز الماك بعد إغلاق القاعات." : "أُلغِي اختيار الشهادة."), await n();
						} catch (e) {
							p(e.message || "تعذر حفظ الشهادة.");
						} finally {
							l(!1);
						}
					},
					children: c ? "جارٍ اختيار وفحص الشهادة…" : "اختيار شهادة البوابة وتجديدها"
				})
			] }),
			i === "legacy" && /* @__PURE__ */ (0, M.jsx)("p", { children: "الدخول المحلي لا تنتهي صلاحيته بانتهاء شهادة. أي شهادة محفوظة تظل موجودة إذا اخترت تفعيل الدخول الآمن لاحقًا." }),
			f && /* @__PURE__ */ (0, M.jsx)("p", {
				role: "status",
				children: f
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الطريقتان تعملان داخل واي فاي الامتحان بلا إنترنت. ظهور الإشعار يعتمد على الموبايل في الحالتين؛ اختبر الأجهزة واحتفظ بالرابط والرمز للدخول."
			})
		]
	});
}
//#endregion
//#region src/NetworkDiagnostics.jsx
var P = {
	probe: "فحص الدخول التلقائي",
	student_page: "فتح صفحة الطالب",
	room_connection: "الاتصال بالقاعة",
	student_join: "طلب دخول طالب",
	answer_save: "حفظ أو تسليم إجابات",
	request_error: "طلب لم يكتمل",
	asset_error: "تعذّر تحميل ملف الصفحة",
	student_session: "تعذّر تحديث حالة الطالب",
	capport: "اكتشاف البوابة المعيارية"
}, ve = {
	request_canceled: "الطلب اتلغى أو اتقطع من جهة الاتصال",
	connection_refused: "خادم الامتحان رفض الاتصال",
	descriptor_limit: "نفدت اتصالات النظام المتاحة",
	connection_reset: "الاتصال اتقطع فجأة",
	connection_closed: "الاتصال اتقفل قبل اكتمال الرد",
	timeout: "انتهت مهلة الاتصال",
	transport_error: "خطأ اتصال يحتاج مراجعة السجل"
};
function ye(e) {
	let t = new Date(e);
	return e && !Number.isNaN(t.getTime()) ? t.toLocaleString("ar-EG") : "غير متاح";
}
function be() {
	let [e, t] = (0, _.useState)(null), [n, r] = (0, _.useState)(!1), [i, a] = (0, _.useState)(""), [o, s] = (0, _.useState)(!1), [c, l] = (0, _.useState)(""), u = (0, _.useRef)(!1), d = async (e) => {
		if (!u.current) {
			u.current = !0, r(!0), a("");
			try {
				let n = window.pywebview?.api;
				if (e === "read") t(await n.network_diagnostics()), s(!1);
				else {
					let r = await (e === "mark" ? n.mark_network_incident() : n.export_network_diagnostics());
					a(r.message || (r.ok ? "تم." : "تعذر إكمال الطلب.")), e === "mark" && r.ok && (t(await n.network_diagnostics()), s(!1));
				}
			} catch {
				e !== "export" && s(!0), a("تعذر قراءة أو حفظ التقرير. حاول مرة أخرى من برنامج مسار على الماك.");
			} finally {
				u.current = !1, r(!1);
			}
		}
	}, f = e?.networkTimeline, p = f?.recording, m = Array.isArray(e?.events) ? e.events : [], h = e?.backendDiagnostics, g = h?.snapshot?.recorder, v = g && (g.persistence !== "ready" || g.dropped > 0 || g.writeFailures > 0 || g.clientRejectedBatches > 0), y = h?.historical === !0, b = !h || h.status === "unavailable" || y;
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "notice network-diagnostics",
		"aria-label": "تشخيص الاتصال والأخطاء",
		children: [
			/* @__PURE__ */ (0, M.jsx)("strong", { children: "تشخيص الاتصال والأخطاء" }),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "التسجيل يعمل تلقائيًا في الخلفية أثناء فتح البرنامج، حتى لو خرجت من شاشة الإعدادات. عند حدوث مشكلة اضغط «سجّل وقت المشكلة»، ثم نزّل التقرير وأرسله للدعم. لا يحتاج إنترنت."
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "actions",
				children: [
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: n,
						onClick: () => d("mark"),
						children: "سجّل وقت المشكلة"
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: n,
						onClick: () => d("export"),
						children: "تنزيل تقرير التشخيص"
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button ghost",
						disabled: n,
						onClick: () => d("read"),
						children: "تحديث السجل"
					})
				]
			}),
			i && /* @__PURE__ */ (0, M.jsx)("p", {
				role: "status",
				children: i
			}),
			e && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
				o && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "المعروض آخر قراءة سابقة؛ تعذر تحديثها ولا تؤكد أن التسجيل يعمل الآن."
				}),
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "network-setup-status",
					"aria-label": "حالة تسجيل التشخيص",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("span", { children: ["سجل الهاب والعناوين: ", /* @__PURE__ */ (0, M.jsx)("b", { children: o ? "قراءة سابقة" : f?.status === "ok" && p?.active ? "يسجل" : f?.status === "partial" ? "السجل غير مكتمل" : "لم يتم التأكد" })] }),
						/* @__PURE__ */ (0, M.jsxs)("span", { children: ["سجل خادم الامتحان: ", /* @__PURE__ */ (0, M.jsx)("b", { children: o ? "قراءة سابقة" : y && h?.status === "partial" ? "متاح من سجل سابق" : b ? "غير متاح في هذا الفحص" : v || h?.status === "partial" ? "السجل غير مكتمل" : "متاح" })] }),
						/* @__PURE__ */ (0, M.jsxs)("span", { children: ["سجل بوابة الدخول: ", /* @__PURE__ */ (0, M.jsx)("b", { children: o ? "قراءة سابقة" : e.loggingInstalled ? "موجود" : "لم يُنشأ بعد" })] })
					]
				}),
				/* @__PURE__ */ (0, M.jsxs)("p", {
					className: "help",
					children: [
						"آخر فحص مسجل للهاب: ",
						ye(p?.lastSampleAt),
						". آخر قراءة للتقرير: ",
						ye(e.capturedAt),
						"."
					]
				}),
				(!f || f.status !== "ok" || p?.writeFailureCount > 0) && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "تعذر تأكيد اكتمال تسجيل حالة الشبكة. التقرير يوضح الأجزاء المتاحة وأخطاء التسجيل؛ راجع أيضًا المساحة الحرة على الماك."
				}),
				b && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "لم نتمكن من قراءة سجل الخادم الآن. يمكنك تنزيل بقية التقرير؛ هذا لا يثبت أن الخادم كان متوقفًا وقت المشكلة."
				}),
				h?.historical && h?.status === "partial" && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "التقرير يتضمن أحداث الخادم المحفوظة سابقًا، مع توقيت كل حدث؛ وهي لا تؤكد حالته الحالية."
				}),
				v && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "حصل تعثّر في تسجيل بعض الأحداث بسبب ضغط التسجيل أو خطأ كتابة. التقرير يتضمن أعداد الأحداث والدفعات المتأثرة وحالة التخزين."
				}),
				!e.loggingInstalled && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "إذا لم يظهر سجل البوابة بعد استخدام هاتف، حدّث تجهيز الماك بعد انتهاء الامتحان وإغلاق القاعات."
				}),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "التقرير يجمع تغيّرات الهاب وعنوان الماك وخدمة توزيع العناوين، وطلبات الدخول والحفظ والأخطاء التي وصلت من المتصفح. أحداث الهاتف غير المتصل تصل بعد رجوع الاتصال إذا أمكن تشغيل الصفحة وحفظ السجل محليًا."
				}),
				/* @__PURE__ */ (0, M.jsxs)("details", { children: [
					/* @__PURE__ */ (0, M.jsx)("summary", { children: "طلبات بوابة الدخول" }),
					/* @__PURE__ */ (0, M.jsxs)("label", { children: ["تصفية بعنوان الموبايل", /* @__PURE__ */ (0, M.jsx)("input", {
						value: c,
						onChange: (e) => l(e.target.value),
						placeholder: "10.77.0.20",
						dir: "ltr"
					})] }),
					/* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "آخر ٢٠٠ طلب مسجل. رد 302 يعني تجهيز التحويل؛ رد 200 لا يثبت وحده أن الهاتف استلم الرد كاملًا. الطلب الصادر من الماك نفسه قد يكون فحص البرنامج."
					}),
					/* @__PURE__ */ (0, M.jsx)("div", {
						style: {
							overflowX: "auto",
							maxHeight: 320
						},
						children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
							/* @__PURE__ */ (0, M.jsx)("th", { children: "الوقت" }),
							/* @__PURE__ */ (0, M.jsx)("th", { children: "الجهاز" }),
							/* @__PURE__ */ (0, M.jsx)("th", { children: "الطلب" }),
							/* @__PURE__ */ (0, M.jsx)("th", { children: "الرد" })
						] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: m.filter((e) => !c || e.ip === c.trim()).slice(-200).reverse().map((e, t) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
							/* @__PURE__ */ (0, M.jsx)("td", { children: ye(e.time) }),
							/* @__PURE__ */ (0, M.jsx)("td", {
								dir: "ltr",
								children: e.ip
							}),
							/* @__PURE__ */ (0, M.jsx)("td", { children: P[e.kind] || "طلب آخر" }),
							/* @__PURE__ */ (0, M.jsxs)("td", { children: [
								e.status,
								" · ",
								e.durationMs,
								"ms",
								e.proxyError && /* @__PURE__ */ (0, M.jsx)("small", {
									style: { display: "block" },
									children: ve[e.proxyError] || "خطأ اتصال غير مصنّف"
								})
							] })
						] }, `${e.time}-${t}`)) })] })
					}),
					!m.length && /* @__PURE__ */ (0, M.jsx)("p", { children: "لا توجد طلبات مسجلة بعد. عدم وصول طلب يحتاج فحص اتصال الهاتف بالشبكة." })
				] })
			] }),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "السجلات محدودة الحجم وتستبدل الأقدم تلقائيًا. التقرير لا يضم أسماء الطلاب أو أرقامهم أو أكوادهم أو إجاباتهم؛ يتضمن عناوين الشبكة المحلية. لا تغيّر متصفح الطالب أو تمسح بياناته أثناء وجود إجابات لم تصل بعد."
			})
		]
	});
}
//#endregion
//#region src/NetworkSetup.jsx
function xe() {
	let e = pe(), t = me(e), n = t || "http://10.77.0.1/", [r, i] = (0, _.useState)(!!window.pywebview?.api?.network_status), [a, o] = (0, _.useState)(!1), [s, c] = (0, _.useState)(!1), l = (0, _.useRef)(!1), [u, d] = (0, _.useState)(""), f = async (e = !1) => {
		let t = window.pywebview?.api;
		if (t?.network_status && !l.current) {
			l.current = !0, c(!0);
			try {
				i(!0), de(await (e && t.refresh_network_status ? t.refresh_network_status() : t.network_status()));
			} catch (e) {
				d(e.message || "تعذر قراءة حالة الشبكة.");
			} finally {
				l.current = !1, c(!1);
			}
		}
	};
	return (0, _.useEffect)(() => {
		let e = () => {
			i(!0), f();
		};
		return window.addEventListener("pywebviewready", e), f(), () => window.removeEventListener("pywebviewready", e);
	}, []), /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel network-setup",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "section-top",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: "شبكة الامتحان بلا راوتر"
				}),
				/* @__PURE__ */ (0, M.jsx)("h2", { children: "تهيئة الاتصال المباشر بالـOmada" }),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "نسخة الإعداد 0.4.24 — الماك يوزّع عناوين الطلاب، والـOmada يوفّر الواي فاي. لا يحتاج الامتحان إنترنت."
				})
			] }), /* @__PURE__ */ (0, M.jsx)("span", {
				className: `badge ${e?.ready ? "complete" : "quiet"}`,
				children: e?.ready ? "الماك جاهز" : !e || e.inspectionError ? "لم يتم التأكد" : "لم تكتمل التهيئة"
			})]
		}), r ? e && !e.supported ? /* @__PURE__ */ (0, M.jsx)("p", {
			className: "help",
			children: e.message
		}) : /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
			/* @__PURE__ */ (0, M.jsxs)("ol", {
				className: "network-setup-steps",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "وصّل الهاب والكابل أولًا" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "وصّل الهاب بالماك وكابل الـOmada بمدخل Ethernet في الهاب، واترك كهرباء الـOmada موصولة. افصل كابلات Ethernet الأخرى أثناء التهيئة لتحديد منفذ الامتحان بوضوح. يمكنك إبقاء الماك على واي فاي آخر للإنترنت." })] }),
					/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "جهّز الماك" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "راجع منفذ الكابل الظاهر أدناه، ثم اضغط زر التهيئة. سيطلب macOS كلمة سر الجهاز لتغيير إعداد منفذ Ethernet وتشغيل خدمتين محليتين. لا تُحفظ كلمة السر في البرنامج." })] }),
					/* @__PURE__ */ (0, M.jsxs)("li", { children: [
						/* @__PURE__ */ (0, M.jsx)("strong", { children: "اختبر صفحة الطلاب" }),
						/* @__PURE__ */ (0, M.jsxs)("p", { children: [
							"اتصل بواي فاي الامتحان، ثم افتح الرابط في Chrome أو Samsung Internet: ",
							/* @__PURE__ */ (0, M.jsx)("b", {
								dir: "ltr",
								children: n
							}),
							" أو امسح QR الموجود في شاشة الامتحان قبل تسجيل الدخول. استمر في نفس الرابط والمتصفح حتى التسليم."
						] }),
						t && /* @__PURE__ */ (0, M.jsxs)("p", { children: [
							"لو صفحة الدخول التلقائي لم تظهر، افتح نفس الرابط أعلاه. الرابط المحلي ",
							/* @__PURE__ */ (0, M.jsx)("b", {
								dir: "ltr",
								children: "http://10.77.0.1/"
							}),
							" بديل قبل تسجيل الدخول فقط؛ لا تنتقل إليه بعد بدء المحاولة."
						] })
					] })
				]
			}),
			/* @__PURE__ */ (0, M.jsxs)("details", {
				className: "notice",
				children: [
					/* @__PURE__ */ (0, M.jsx)("summary", { children: "سامسونج متصل لكن نافذة الدخول لم تظهر؟" }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: "لو الرابط يفتح يدويًا، اتصال الموبايل بالامتحان يعمل؛ النافذة التلقائية تعتمد على نظامه. افتح الامتحان من المتصفح العادي قبل تسجيل الدخول، واستمر في نفس المتصفح حتى التسليم." }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: "إذا كان الاتصال يتبدل: إعدادات الموبايل ← الاتصالات ← Wi-Fi ← ⋮ ← Intelligent Wi-Fi، وأوقف «Switch to mobile data» أثناء الامتحان، واختر البقاء متصلًا عند ظهور «بلا إنترنت». أسماء الخيارات تختلف حسب الموديل." }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: "لو الرابط نفسه لا يفتح، راجع وصول شبكة الطلاب إلى الماك، وإعداد Guest Network أو قواعد الحظر في Omada. نجاح الفحص على الماك وحده لا يثبت وصول الموبايلات." })
				]
			}),
			e?.inspectionError && /* @__PURE__ */ (0, M.jsx)("p", {
				className: "notice",
				role: "status",
				children: e.message
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "network-setup-status",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("span", { children: ["منفذ الكابل: ", /* @__PURE__ */ (0, M.jsx)("b", { children: !e || e.inspectionError ? "لم يتم التأكد" : e.ethernetAmbiguous ? "أكثر من منفذ متصل" : e.ethernetActive ? "متصل" : "غير متصل" })] }),
					/* @__PURE__ */ (0, M.jsxs)("span", { children: ["توزيع العناوين: ", /* @__PURE__ */ (0, M.jsx)("b", { children: !e || e.inspectionError ? "لم يتم التأكد" : e.dhcpRunning ? e.dhcpConfigurationReady === !1 ? "الإعداد يحتاج مراجعة" : e.dhcpSocketReady === !0 ? "المنفذ يستقبل" : e.dhcpSocketReady === !1 ? "المنفذ لا يستقبل" : "لم يتم التأكد" : "الخدمة متوقفة" })] }),
					/* @__PURE__ */ (0, M.jsxs)("span", { children: ["صفحة الطلاب: ", /* @__PURE__ */ (0, M.jsx)("b", { children: e?.androidProbe?.studentServiceReady ? "تستجيب" : "لم يتم التأكد" })] })
				]
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "notice",
				children: [
					/* @__PURE__ */ (0, M.jsx)("strong", { children: "فحص خدمة الدخول والامتحان" }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: e?.androidProbe?.detail || "وصّل Omada وجهّز خدمات الماك لتشغيل الفحص." }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: "شبكة ٢.٤: لم يتم التحقق من إعداداتها · شبكة ٥: لم يتم التحقق من إعداداتها" }),
					/* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "فحص خدمة الماك لا يثبت إعداد كل تردد. يلزم قراءة إعدادات Omada وتجربة هاتف متصل بكل شبكة."
					}),
					/* @__PURE__ */ (0, M.jsx)("a", {
						className: "text-button",
						href: "https://10.77.0.2/",
						target: "_blank",
						rel: "noreferrer",
						children: "فتح إعدادات Omada"
					})
				]
			}),
			e?.ethernetService && /* @__PURE__ */ (0, M.jsxs)("p", {
				className: "help",
				children: [
					"منفذ الكابل: ",
					e.ethernetService,
					" (",
					e.ethernetDevice,
					")، العنوان: ",
					e.ethernetIp || "غير محدد"
				]
			}),
			e?.ethernetDetail && /* @__PURE__ */ (0, M.jsx)("p", {
				className: "notice",
				role: "status",
				children: e.ethernetDetail
			}),
			e?.configValid === !1 && /* @__PURE__ */ (0, M.jsxs)("p", {
				className: "help",
				role: "status",
				children: ["فحص إعداد الشبكة: ", e.configDetail]
			}),
			e?.dhcpDetail && /* @__PURE__ */ (0, M.jsxs)("p", {
				className: "help",
				role: "status",
				children: ["فحص توزيع العناوين: ", e.dhcpDetail]
			}),
			e?.dhcpLeases && /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "notice",
				role: "status",
				children: [
					/* @__PURE__ */ (0, M.jsx)("strong", { children: "عناوين أجهزة الطلاب" }),
					/* @__PURE__ */ (0, M.jsx)("p", { children: e.dhcpLeases.detail }),
					e.dhcpLeases.dhcpLeaseCount != null && /* @__PURE__ */ (0, M.jsxs)("p", { children: [
						"المحجوز: ",
						e.dhcpLeases.dhcpLeaseCount,
						" من ",
						e.dhcpLeases.capacity
					] }),
					e.dhcpLeases.findings?.map((e) => /* @__PURE__ */ (0, M.jsx)("p", { children: e.detail }, e.code)),
					/* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "الأجهزة المتصلة تجدّد عنوانها تلقائيًا. فحص السجل لا يثبت غياب راوتر آخر يوزّع عناوين على الشبكة."
					})
				]
			}),
			/* @__PURE__ */ (0, M.jsx)(N, {
				status: e,
				disabled: a,
				refresh: f
			}),
			/* @__PURE__ */ (0, M.jsxs)("details", {
				className: "notice",
				children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "اختبار القاعة قبل دخول الطلاب" }), /* @__PURE__ */ (0, M.jsxs)("ol", { children: [
					/* @__PURE__ */ (0, M.jsx)("li", { children: "تأكد أن الماك هو موزع العناوين الوحيد وأن شبكة الطلاب تسمح بالوصول إليه." }),
					/* @__PURE__ */ (0, M.jsx)("li", { children: "اختبر سامسونج وآيفون وموبايلًا قديمًا على ترددي الواي فاي، من أماكن مختلفة في القاعة." }),
					/* @__PURE__ */ (0, M.jsx)("li", { children: "اختبر إشعار الدخول، ثم دخول الامتحان وحفظ إجابة والتسليم في نفس المتصفح." }),
					/* @__PURE__ */ (0, M.jsx)("li", { children: "اقطع اتصال هاتف التجربة وأعده، وتأكد من وصول إجابته. اختبر عددًا قريبًا من عدد الطلاب الفعلي." }),
					/* @__PURE__ */ (0, M.jsx)("li", { children: "راجع الكهرباء والكابل، وصلاحية الشهادة إذا اخترت الدخول الآمن. احتفظ برابط الطلاب وQR كطريق إضافي للدخول." })
				] })]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "لو عنوان الماك أو توزيع عناوين الطلاب محتاج إصلاح، أغلق القاعات واضغط زر التهيئة. يعيد ضبط منفذ الهاب المتصل ويختبر عنوانه وخدمة التوزيع. لو الهاب مفصول، وصّله واضغط «فحص الاتصال» أولًا."
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "actions",
				children: [
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						disabled: a || s || !e?.ethernetActive || e?.ethernetAmbiguous,
						onClick: async () => {
							o(!0), d("انتظر نافذة macOS واكتب كلمة سر الجهاز فيها فقط…");
							try {
								let e = await window.pywebview.api.prepare_network();
								d(e.message), e.status ? de(e.status) : await f();
							} catch (e) {
								d(e.message || "تعذرت التهيئة.");
							} finally {
								o(!1);
							}
						},
						children: a ? "جارٍ التهيئة…" : e?.ready ? "تحديث تجهيز الماك" : "تهيئة الماك الآن"
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: a || s,
						onClick: () => f(!0),
						children: s ? "جارٍ فحص الاتصال…" : "فحص الاتصال"
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button ghost",
						onClick: () => O(n).catch((e) => d(e.message)),
						children: "نسخ رابط الطلاب"
					})
				]
			}),
			/* @__PURE__ */ (0, M.jsx)(be, {}),
			u && /* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				role: "status",
				children: u
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "لا تفعّل Portal الداخلي في الـOmada لهذا الاختبار؛ سيضيف صفحة موافقة قبل الامتحان. ظهور نافذة الدخول تلقائيًا يعتمد على نظام الموبايل، لذلك احتفظ برابط الطلاب كبديل."
			})
		] }) : /* @__PURE__ */ (0, M.jsx)("p", {
			className: "help",
			children: "افتح هذه الشاشة داخل برنامج مسار على الماك لتفعيل التهيئة."
		})]
	});
}
//#endregion
//#region src/Storage.jsx
var Se = (e) => e >= 1024 ** 3 ? `${b(e / 1024 ** 3)} GB` : e >= 1024 ** 2 ? `${b(e / 1024 ** 2)} MB` : `${b(e / 1024)} KB`, F = (e) => (/* @__PURE__ */ new Date(e * 1e3)).toLocaleString("ar-EG", {
	dateStyle: "medium",
	timeStyle: "short"
});
function Ce({ token: e, onWhatsApp: t, centerIntegrated: n = !1 }) {
	let [r, i] = (0, _.useState)(null), [a, o] = (0, _.useState)(null), [s, c] = (0, _.useState)(""), [l, u] = (0, _.useState)("يمكنك تشغيل فحص بنية الملف بدون تغيير البيانات."), [d, f] = (0, _.useState)(!1), [p, m] = (0, _.useState)(""), h = async () => {
		try {
			let [e, t] = await Promise.all([E("/api/storage"), E("/api/settings/gemini")]);
			i(e), o(t), m("");
		} catch (e) {
			m(e.message);
		}
	};
	(0, _.useEffect)(() => {
		h();
	}, []);
	let g = async (e) => {
		f(!0);
		try {
			await e();
		} catch (e) {
			T(e.message, !0);
		} finally {
			f(!1);
		}
	};
	return p ? /* @__PURE__ */ (0, M.jsxs)("div", {
		className: "empty",
		children: [
			/* @__PURE__ */ (0, M.jsx)("h2", { children: "تعذر قراءة بيانات التخزين" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: p }),
			/* @__PURE__ */ (0, M.jsx)("button", {
				className: "button",
				onClick: h,
				children: "إعادة المحاولة"
			})
		]
	}) : !r || !a ? /* @__PURE__ */ (0, M.jsx)("div", {
		className: "loading",
		role: "status",
		children: "جارٍ قراءة بيانات التخزين…"
	}) : /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "page-heading",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: "كل شيء على جهازك"
				}),
				/* @__PURE__ */ (0, M.jsx)("h1", { children: "الإعدادات والبيانات" }),
				/* @__PURE__ */ (0, M.jsx)("p", { children: "مفتاح التصحيح وبيانات الامتحانات وإعدادات الواتساب محفوظة على هذا الكمبيوتر." })
			] }), /* @__PURE__ */ (0, M.jsx)("button", {
				className: "button",
				disabled: d,
				onClick: () => g(async () => {
					await E("/api/storage/backup", {}, e), await h(), T("تم إنشاء نسخة احتياطية جديدة");
				}),
				children: "إنشاء نسخة احتياطية"
			})]
		}),
		n ? /* @__PURE__ */ (0, M.jsx)("p", {
			className: "notice",
			children: "تهيئة شبكة ويندوز ومتابعة الاتصال من شاشة الامتحانات في برنامج السنتر."
		}) : /* @__PURE__ */ (0, M.jsx)(xe, {}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel gemini-settings",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "section-top",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
						/* @__PURE__ */ (0, M.jsx)("span", {
							className: "context-label",
							children: "إعدادات التصحيح"
						}),
						/* @__PURE__ */ (0, M.jsx)("h2", { children: "مفتاح خدمة التصحيح" }),
						/* @__PURE__ */ (0, M.jsx)("p", {
							className: "help",
							children: "أدخل المفتاح مرة واحدة لتشغيل تصحيح المقالي للجميع بعد انتهاء الامتحان."
						})
					] }), /* @__PURE__ */ (0, M.jsx)("span", {
						className: `badge ${a.configured ? "complete" : "quiet"}`,
						children: a.configured ? "المفتاح محفوظ" : "لم يُضف مفتاح"
					})]
				}),
				/* @__PURE__ */ (0, M.jsxs)("form", {
					onSubmit: (t) => {
						t.preventDefault(), g(async () => {
							await E("/api/settings/gemini", { apiKey: s.trim() }, e), c(""), o({
								...a,
								configured: !0
							}), T("تم حفظ مفتاح التصحيح");
						});
					},
					children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["مفتاح API", /* @__PURE__ */ (0, M.jsx)("input", {
						type: "password",
						autoComplete: "new-password",
						required: !0,
						value: s,
						onChange: (e) => c(e.target.value),
						placeholder: "الصق المفتاح الجديد هنا"
					})] }), /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						type: "submit",
						disabled: d,
						children: "حفظ المفتاح"
					})]
				}),
				/* @__PURE__ */ (0, M.jsxs)("p", {
					className: "help",
					children: [
						"المفتاح يُحفظ في ملف ",
						/* @__PURE__ */ (0, M.jsx)("code", {
							dir: "ltr",
							children: ".env"
						}),
						" داخل مجلد البيانات، خارج SQLite والنسخ الاحتياطية. لا يظهر بعد حفظه."
					]
				}),
				/* @__PURE__ */ (0, M.jsxs)("label", {
					className: "path-label",
					children: ["مكان ملف الإعدادات", /* @__PURE__ */ (0, M.jsx)("input", {
						className: "file-path",
						readOnly: !0,
						dir: "ltr",
						value: a.path
					})]
				})
			]
		}),
		/* @__PURE__ */ (0, M.jsx)("section", {
			className: "panel",
			children: /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "واتساب السنتر الرسمي" }), /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "ربط الحساب، ومزامنة القوالب، ومتابعة إرسال تقارير الطلاب من قائمة واتساب السنتر."
				})] }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					onClick: t,
					children: "فتح إعدادات واتساب"
				})]
			})
		}),
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "storage-layout",
			children: [/* @__PURE__ */ (0, M.jsxs)("section", {
				className: "panel storage-main",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "section-top",
						children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "مكان حفظ البيانات" }), /* @__PURE__ */ (0, M.jsx)("p", {
							className: "help",
							children: "قاعدة محلية مستقلة عن منصة مسار."
						})] }), /* @__PURE__ */ (0, M.jsx)("span", {
							className: "badge complete",
							children: "SQLite · محلي"
						})]
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "path-label",
						children: ["ملف قاعدة البيانات", /* @__PURE__ */ (0, M.jsx)("input", {
							className: "file-path",
							readOnly: !0,
							dir: "ltr",
							value: r.databasePath
						})]
					}),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "actions",
						children: [/* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary small",
							disabled: d,
							onClick: () => g(async () => {
								await E("/api/storage/open-folder", {}, e), T("تم فتح مجلد البيانات");
							}),
							children: "فتح مجلد البيانات ↗"
						}), /* @__PURE__ */ (0, M.jsx)("button", {
							className: "button ghost small",
							onClick: () => O(r.databasePath).catch((e) => T(e.message, !0)),
							children: "نسخ المسار"
						})]
					}),
					/* @__PURE__ */ (0, M.jsxs)("dl", {
						className: "storage-facts",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("dt", { children: "حجم البيانات مع سجل الحفظ" }), /* @__PURE__ */ (0, M.jsx)("dd", {
								dir: "ltr",
								children: Se(r.databaseBytes + r.pendingBytes)
							})] }),
							/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("dt", { children: "المساحة المتاحة على القرص" }), /* @__PURE__ */ (0, M.jsx)("dd", {
								dir: "ltr",
								children: Se(r.freeBytes)
							})] }),
							/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("dt", { children: "الامتحانات المحفوظة" }), /* @__PURE__ */ (0, M.jsx)("dd", { children: b(r.counts.exams) })] }),
							/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("dt", { children: "محاولات الطلاب / المسلّمة" }), /* @__PURE__ */ (0, M.jsxs)("dd", { children: [
								b(r.counts.students),
								" / ",
								b(r.counts.submissions)
							] })] })
						]
					}),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "integrity-row",
						children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "فحص سلامة قاعدة البيانات" }), /* @__PURE__ */ (0, M.jsx)("p", {
							className: "help",
							role: "status",
							children: l
						})] }), /* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary small",
							disabled: d,
							onClick: () => g(async () => {
								u("جارٍ الفحص…");
								let t = await E("/api/storage/check", {}, e);
								u(t.healthy ? "بنية قاعدة البيانات سليمة." : "الفحص اكتشف مشكلة؛ احتفظ بنسخة وراجع الدعم.");
							}),
							children: "تشغيل الفحص"
						})]
					})
				]
			}), /* @__PURE__ */ (0, M.jsxs)("aside", {
				className: "storage-explainer",
				children: [
					/* @__PURE__ */ (0, M.jsx)("h2", { children: "ما الذي يتم حفظه؟" }),
					/* @__PURE__ */ (0, M.jsxs)("ul", { children: [
						/* @__PURE__ */ (0, M.jsx)("li", { children: "الأسئلة والإجابات النموذجية وإعدادات الوقت." }),
						/* @__PURE__ */ (0, M.jsx)("li", { children: "بيانات الطلاب وإجاباتهم التي وصلت للكمبيوتر." }),
						/* @__PURE__ */ (0, M.jsx)("li", { children: "التسليم والدرجات وملاحظات التصحيح." })
					] }),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "storage-tip",
						children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "احتفظ بنسخة خارج الكمبيوتر" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "النسخ الموجودة هنا على نفس القرص. نزّل نسخة إلى فلاشة أو قرص آخر بعد كل جلسة." })]
					})
				]
			})]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel backups-panel",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "section-top padded",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "النسخ المحفوظة" }), /* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "كل خمس دقائق أثناء التشغيل، وعند التشغيل والإغلاق وتغيير حالة الجلسة."
					})] }), /* @__PURE__ */ (0, M.jsxs)("span", {
						className: "badge quiet",
						children: [
							b(r.backupCount),
							" نسخة · ",
							Se(r.backupBytes)
						]
					})]
				}),
				/* @__PURE__ */ (0, M.jsx)("div", {
					className: "table-scroll",
					children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
						/* @__PURE__ */ (0, M.jsx)("th", { children: "تاريخ النسخة" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الحجم" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الملف" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "تنزيل" })
					] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: r.backups.length ? r.backups.map((e, t) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
						/* @__PURE__ */ (0, M.jsxs)("td", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: F(e.createdAt) }), t === 0 && /* @__PURE__ */ (0, M.jsx)("small", { children: "أحدث نسخة" })] }),
						/* @__PURE__ */ (0, M.jsx)("td", {
							dir: "ltr",
							children: Se(e.bytes)
						}),
						/* @__PURE__ */ (0, M.jsx)("td", { children: /* @__PURE__ */ (0, M.jsx)("span", {
							className: "backup-filename",
							dir: "ltr",
							children: e.name
						}) }),
						/* @__PURE__ */ (0, M.jsx)("td", { children: /* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary small",
							disabled: d,
							onClick: () => g(() => A(`/api/backups/${encodeURIComponent(e.name)}`, e.name, "application/octet-stream")),
							children: "تنزيل النسخة ↓"
						}) })
					] }, e.name)) : /* @__PURE__ */ (0, M.jsx)("tr", { children: /* @__PURE__ */ (0, M.jsx)("td", {
						className: "empty",
						colSpan: "4",
						children: "لا توجد نسخ بعد. أنشئ أول نسخة الآن."
					}) }) })] })
				}),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help padded",
					children: "نعرض أحدث ٣٠ نسخة. النسخ الأقدم تظل في مجلد backups."
				})
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("details", {
			className: "restore-guide",
			children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "إزاي أسترجع نسخة احتياطية؟" }), /* @__PURE__ */ (0, M.jsxs)("ol", { children: [
				n ? /* @__PURE__ */ (0, M.jsx)("li", { children: "أنه كل قاعات الامتحان، ونزّل النسخة المطلوبة واحتفظ بمسار مجلد البيانات. أعد تشغيل ويندوز، ولا تفتح برنامج السنتر قبل إنهاء الخطوات التالية؛ إغلاق نافذة السنتر وحده يترك خدمة الامتحان شغالة." }) : /* @__PURE__ */ (0, M.jsx)("li", { children: "نزّل النسخة المطلوبة وافتح مجلد البيانات، ثم أغلق البرنامج." }),
				/* @__PURE__ */ (0, M.jsx)("li", { children: "احتفظ بالمجلد الحالي باسم مختلف، وأنشئ مجلدًا فارغًا بنفس الاسم." }),
				/* @__PURE__ */ (0, M.jsxs)("li", { children: [
					"ضع النسخة في المجلد الجديد باسم ",
					/* @__PURE__ */ (0, M.jsx)("b", {
						dir: "ltr",
						children: "exams.sqlite3"
					}),
					"، ثم افتح البرنامج وراجع البيانات."
				] })
			] })]
		})
	] });
}
//#endregion
//#region src/whatsappTemplateFields.js
var we = /\{\{\s*([0-9]+|[a-z][a-z0-9_]*)\s*\}\}/g, Te = {
	1: "name",
	2: "lesson",
	3: "month",
	4: "center",
	5: "time",
	6: "score",
	7: "maximum",
	8: "homework"
}, Ee = {
	student_name: "name",
	name: "name",
	student_code: "code",
	code: "code",
	score: "score",
	max_score: "maximum",
	maximum: "maximum",
	result: "result",
	exam_name: "exam",
	lesson: "lesson",
	month: "month",
	center: "center",
	group: "group",
	group_name: "group",
	time: "time",
	homework: "homework",
	homework_status: "homework",
	attendance: "attendance",
	attendance_status: "attendance"
}, De = {
	name: "اسم الطالب",
	code: "كود الطالب",
	result: "جاب كام من كام",
	score: "الدرجة",
	maximum: "الدرجة النهائية",
	exam: "اسم الامتحان",
	lesson: "الحصة",
	month: "الشهر",
	center: "السنتر",
	group: "المجموعة",
	time: "موعد الحصة",
	homework: "حالة الواجب",
	attendance: "حالة الحضور",
	literal: "نص ثابت"
};
function Oe(e) {
	if (Array.isArray(e?.reportFields)) return e.reportFields;
	let t = [], n = (n, r, i) => {
		let a = [...new Set([...String(r || "").matchAll(we)].map((e) => e[1]))];
		a.every((e) => /^\d+$/.test(e)) && a.sort((e, t) => Number(e) - Number(t));
		for (let r of a) t.push({
			scope: n,
			position: r,
			buttonIndex: i,
			key: n === "button" ? `button:${i}:${r}` : `${n}:${r}`,
			suggestedSource: Ee[r] || (["parent_follow_center", "parent__follow_center"].includes(e?.name) ? n === "header" ? "name" : n === "body" ? Te[r] : "" : "")
		});
	};
	for (let t of e?.components || []) t.type === "HEADER" && t.format === "TEXT" && n("header", t.text), t.type === "BODY" && n("body", t.text), t.type === "BUTTONS" && t.buttons?.forEach((e, t) => {
		e.type === "URL" && n("button", e.url, t);
	});
	return t;
}
function ke(e, t, n) {
	return e?.savedBindings?.find((r) => r.bindingSchema === e.bindingSchema && r.recipientKind === t && r.reportKind === n && Ae(e, r.mappings));
}
function Ae(e, t) {
	return Array.isArray(t) && t.length === Oe(e).length && t.every((e) => Object.hasOwn(De, e?.source) && (e.source !== "literal" || String(e.text || "").trim().length > 0));
}
function je(e, t, n) {
	let r = ke(e, t, n);
	return r ? r.mappings.map((e) => ({ ...e })) : Oe(e).map((e) => ({ source: e.suggestedSource || "" }));
}
function Me(e, t, n = window.location.search) {
	let r = new URLSearchParams(n);
	return r.set("view", e), r.set("batch", t), `?${r.toString()}`;
}
function Ne(e, t) {
	let n = Oe(e), r = (e, r, i) => String(r || "").replace(we, (r, a) => {
		let o = n.findIndex((t) => t.scope === e && String(t.position) === a && (e !== "button" || t.buttonIndex === i));
		return o < 0 ? "" : t.parameters?.[o] || "";
	});
	return (e?.components || []).flatMap((e) => e.type === "HEADER" && e.format === "TEXT" ? [r("header", e.text)] : e.type === "BODY" ? [r("body", e.text)] : e.type === "FOOTER" ? [e.text] : []).join("\n");
}
//#endregion
//#region src/WhatsApp.jsx
var Pe = {
	accepted: "قبل واتساب الرسالة",
	failed: "لم تُرسل",
	rejected: "رفض واتساب الطلب",
	unknown: "غير مؤكدة — راجع واتساب",
	preparing: "تجهيز التقرير",
	sending: "جارٍ الإرسال"
}, Fe = (e) => (e?.components || []).find((e) => e.type === "BODY")?.text || "";
function Ie({ templates: e, templateId: t, setTemplateId: n, mappings: r, setMappings: i, disabled: a = !1, recipientKind: o = "student", reportKind: s = "exam" }) {
	let c = e.find((e) => e.id === t && e.reportSupported), l = Oe(c), u = ke(c, o, s), d = e.filter((e) => e.reportSupported), f = !!(u && JSON.stringify(u.mappings) === JSON.stringify(r)), p = (t) => {
		n(t), i(je(e.find((e) => e.id === t), o, s));
	}, m = (e, t) => i(l.map((n, i) => i === e ? {
		...r[i],
		...t
	} : r[i] || { source: "" }));
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["قالب رسالة التقرير", /* @__PURE__ */ (0, M.jsxs)("select", {
		className: "whatsapp-template-list",
		size: Math.max(2, Math.min(6, d.length + 1)),
		disabled: a,
		value: t,
		onChange: (e) => p(e.target.value),
		children: [/* @__PURE__ */ (0, M.jsx)("option", {
			value: "",
			children: "اختر قالبًا معتمدًا من الحساب"
		}), d.map((e) => /* @__PURE__ */ (0, M.jsxs)("option", {
			value: e.id,
			children: [
				e.name,
				" · ",
				e.language
			]
		}, e.id))]
	})] }), c && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("div", {
		className: "whatsapp-message",
		dir: "auto",
		children: Fe(c)
	}), /* @__PURE__ */ (0, M.jsxs)("details", {
		open: !u || void 0,
		children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: f ? "ربط المتغيرات مثبت · تعديل الربط" : "حدد بيانات متغيرات القالب" }), /* @__PURE__ */ (0, M.jsx)("div", {
			className: "whatsapp-fields",
			children: l.map((e, t) => /* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: [
				e.scope === "header" ? "العنوان" : e.scope === "button" ? "زر الرابط" : "نص الرسالة",
				" · المتغير ",
				e.position,
				/* @__PURE__ */ (0, M.jsxs)("select", {
					disabled: a,
					value: r[t]?.source || "",
					onChange: (e) => m(t, { source: e.target.value }),
					children: [/* @__PURE__ */ (0, M.jsx)("option", {
						value: "",
						children: "اختر بيانات الطالب"
					}), Object.entries(De).map(([e, t]) => /* @__PURE__ */ (0, M.jsx)("option", {
						value: e,
						children: t
					}, e))]
				})
			] }), r[t]?.source === "literal" && /* @__PURE__ */ (0, M.jsxs)("label", { children: ["النص الثابت", /* @__PURE__ */ (0, M.jsx)("input", {
				disabled: a,
				maxLength: 1e3,
				value: r[t]?.text || "",
				onChange: (e) => m(t, { text: e.target.value })
			})] })] }, e.key))
		})]
	}, `${c.id}:${c.bindingSchema}:${!!u}`)] })] });
}
function Le({ token: e, returnBatchId: t = "" }) {
	let [n, r] = (0, _.useState)(null), [i, a] = (0, _.useState)([]), [o, s] = (0, _.useState)([]), [c, l] = (0, _.useState)(""), [u, d] = (0, _.useState)(""), [f, p] = (0, _.useState)({
		phoneNumberId: "",
		businessAccountId: "",
		apiVersion: "v23.0",
		accessToken: "",
		appSecret: ""
	}), [m, h] = (0, _.useState)(!1), [g, v] = (0, _.useState)(""), [y, x] = (0, _.useState)(""), S = async () => {
		let [e, t, n] = await Promise.all([
			E("/api/whatsapp/configuration"),
			E("/api/whatsapp/templates", void 0, void 0, { timeoutMs: 12e4 }),
			E("/api/whatsapp/history")
		]);
		r(e), a(t.templates), x(t.syncError || ""), s(n.items), p((t) => ({
			...t,
			phoneNumberId: e.phoneNumberId || "",
			businessAccountId: e.businessAccountId || "",
			apiVersion: e.apiVersion || "v23.0",
			accessToken: "",
			appSecret: ""
		})), v("");
	};
	(0, _.useEffect)(() => {
		S().catch((e) => v(e.message));
	}, []), (0, _.useEffect)(() => {
		if (m) return;
		let e = !0, t = setInterval(() => {
			E("/api/whatsapp/templates", void 0, void 0, { timeoutMs: 12e4 }).then((t) => {
				e && (a(t.templates), x(t.syncError || ""));
			}).catch(() => {});
		}, 6e4);
		return () => {
			e = !1, clearInterval(t);
		};
	}, [m]);
	let C = async (e) => {
		let t = e.target.files?.[0];
		if (e.target.value = "", t) try {
			if (t.size > 65536) throw Error("ملف الحساب أكبر من المسموح");
			let e = JSON.parse(await t.text()), n = [
				"phoneNumberId",
				"businessAccountId",
				"apiVersion",
				"accessToken",
				"appSecret"
			];
			if (!e || typeof e != "object" || Array.isArray(e)) throw Error();
			let r = Object.fromEntries(n.filter((t) => typeof e[t] == "string").map((t) => [t, e[t].trim()]));
			if (!r.phoneNumberId || !r.businessAccountId || !r.accessToken) throw Error();
			if (/^v(?:[0-9]|1[0-9]|20)\.0$/.test(r.apiVersion || "")) {
				let e = r.apiVersion;
				r.apiVersion = "v23.0", d(`نسخة الملف ${e} قديمة؛ انتهى دعم v20.0 في ٢٤ سبتمبر ٢٠٢٦. اخترنا v23.0 المدعوم حتى ٨ أكتوبر ٢٠٢٧. راجع النسخة وبيانات الحساب قبل الضغط على حفظ.`);
			} else d("");
			p((e) => ({
				...e,
				...r
			})), l(""), T("تمت قراءة الحساب؛ اضغط حفظ الحساب. لا يُرسل أي واتساب بمجرد الحفظ.");
		} catch {
			l("اختر ملف JSON صحيحًا يحتوي بيانات حساب واتساب.");
		}
	}, w = /^v(?:[0-9]|1[0-9]|20)\.0$/.test(f.apiVersion), D = async (e) => {
		h(!0);
		try {
			await e();
		} catch (e) {
			T(e.message, !0);
		} finally {
			h(!1);
		}
	};
	return g ? /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel whatsapp-settings",
		children: [
			/* @__PURE__ */ (0, M.jsx)("h1", { children: "تعذر قراءة إعدادات واتساب" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: g }),
			/* @__PURE__ */ (0, M.jsx)("button", {
				className: "button",
				onClick: () => S().catch((e) => v(e.message)),
				children: "إعادة المحاولة"
			})
		]
	}) : n ? /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "page-heading",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: "تقارير الطلاب"
				}),
				/* @__PURE__ */ (0, M.jsx)("h1", { children: "واتساب السنتر" }),
				t && /* @__PURE__ */ (0, M.jsx)("a", {
					className: "button secondary",
					href: Me("whatsapp-center", t),
					children: "العودة لإرسال تقرير السنتر"
				}),
				/* @__PURE__ */ (0, M.jsx)("p", { children: "اربط الحساب، واختر القالب المعتمد. إرسال التقارير متاح من نتائج القاعة أو الشيتات." })
			] }), /* @__PURE__ */ (0, M.jsx)("span", {
				className: `badge ${n.configured ? "complete" : "quiet"}`,
				children: n.configured ? "بيانات الحساب محفوظة" : "أكمل ربط الحساب"
			})]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel whatsapp-settings",
			children: [
				/* @__PURE__ */ (0, M.jsx)("h2", { children: "الحساب الرسمي" }),
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "text-button",
					onClick: () => O("https://developers.facebook.com/docs/graph-api/changelog/versions/"),
					children: "نسخ رابط إصدارات Meta الرسمي"
				}),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["قراءة ملف إعدادات الحساب JSON", /* @__PURE__ */ (0, M.jsx)("input", {
					type: "file",
					accept: ".json,application/json",
					disabled: m,
					onChange: C
				})] }),
				c && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "notice",
					children: c
				}),
				u && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "notice",
					role: "status",
					children: u
				}),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "انسخ الرقمين المختلفين من WhatsApp ← API Setup. يلزم إنترنت على جهاز الإدارة للمزامنة والإرسال."
				}),
				w && /* @__PURE__ */ (0, M.jsxs)("p", {
					className: "notice",
					role: "alert",
					children: ["ملف الحساب يستخدم نسخة Meta API قديمة؛ انتهى دعم v20.0 في ٢٤ سبتمبر ٢٠٢٦. v23.0 مدعوم حتى ٨ أكتوبر ٢٠٢٧.", /* @__PURE__ */ (0, M.jsx)("button", {
						type: "button",
						className: "text-button",
						disabled: m,
						onClick: () => p((e) => ({
							...e,
							apiVersion: "v23.0"
						})),
						children: "اختيار v23.0 لهذا الحساب"
					})]
				}),
				/* @__PURE__ */ (0, M.jsxs)("form", {
					onSubmit: (t) => {
						t.preventDefault(), D(async () => {
							await E("/api/whatsapp/configuration", f, e), await S(), T("تم حفظ إعدادات واتساب");
						});
					},
					children: [
						/* @__PURE__ */ (0, M.jsx)("div", {
							className: "whatsapp-fields",
							children: [
								["phoneNumberId", "Phone Number ID"],
								["businessAccountId", "WhatsApp Business Account ID"],
								["apiVersion", "نسخة Meta API"]
							].map(([e, t]) => /* @__PURE__ */ (0, M.jsxs)("label", { children: [t, /* @__PURE__ */ (0, M.jsx)("input", {
								required: !0,
								dir: "ltr",
								value: f[e],
								onChange: (t) => p({
									...f,
									[e]: t.target.value.trim()
								})
							})] }, e))
						}),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: [
							"توكن واتساب",
							n.tokenConfigured ? " · محفوظ" : "",
							/* @__PURE__ */ (0, M.jsx)("input", {
								type: "password",
								autoComplete: "off",
								required: !n.tokenConfigured,
								value: f.accessToken,
								onChange: (e) => p({
									...f,
									accessToken: e.target.value.trim()
								}),
								placeholder: n.tokenConfigured ? "اتركه فارغًا لاستخدام المحفوظ" : "الصق التوكن هنا"
							})
						] }),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: [
							"App Secret",
							n.appSecretConfigured ? " · محفوظ" : "",
							/* @__PURE__ */ (0, M.jsx)("input", {
								type: "password",
								autoComplete: "off",
								value: f.appSecret,
								onChange: (e) => p({
									...f,
									appSecret: e.target.value.trim()
								}),
								placeholder: "سر تطبيق Meta الخاص بالويب هوك"
							})
						] }),
						/* @__PURE__ */ (0, M.jsx)("p", {
							className: "help",
							children: "المفاتيح محمية بحساب مستخدم ويندوز الحالي، خارج قاعدة البيانات والنسخ الاحتياطية. عند نقل البرنامج إلى جهاز آخر، اربط الحساب عليه مرة أخرى."
						}),
						/* @__PURE__ */ (0, M.jsx)("button", {
							className: "button",
							disabled: m || w,
							children: "حفظ الحساب"
						})
					]
				})
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel whatsapp-settings",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "section-top",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "القوالب الرسمية" }), /* @__PURE__ */ (0, M.jsxs)("p", {
						className: "help",
						children: [
							b(i.length),
							" قالب · ",
							b(i.filter((e) => e.reportSupported).length),
							" متاح للتقارير النصية أو PDF. يقرأ البرنامج متغيرات كل قالب تلقائيًا، ويمكنك تغيير ربطها قبل الإرسال."
						]
					})] }), /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: m || !n.configured,
						onClick: () => D(async () => {
							await E("/api/whatsapp/templates/sync", {}, e, { timeoutMs: 12e4 }), await S(), T("تم تحديث القوالب والتحقق من رقم الإرسال");
						}),
						children: "مزامنة القوالب من Meta"
					})]
				}),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "تُحدّث القوالب تلقائيًا كل ٥ دقائق ما دامت الشاشة مفتوحة، وقبل المعاينة والإرسال عند الحاجة."
				}),
				y && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "notice",
					children: y
				}),
				/* @__PURE__ */ (0, M.jsx)("div", {
					className: "whatsapp-templates",
					children: i.length ? i.map((e) => /* @__PURE__ */ (0, M.jsxs)("article", {
						className: "whatsapp-template",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("div", {
								className: "section-top",
								children: [/* @__PURE__ */ (0, M.jsx)("strong", {
									dir: "ltr",
									children: e.name
								}), /* @__PURE__ */ (0, M.jsx)("span", {
									className: `badge ${e.reportSupported ? "complete" : "quiet"}`,
									children: e.status === "APPROVED" ? "معتمد" : e.status
								})]
							}),
							/* @__PURE__ */ (0, M.jsxs)("p", {
								className: "help",
								children: [
									e.language,
									" · ",
									e.category
								]
							}),
							/* @__PURE__ */ (0, M.jsx)("p", {
								className: "whatsapp-message",
								dir: "auto",
								children: Fe(e)
							}),
							/* @__PURE__ */ (0, M.jsx)("p", {
								className: "help",
								children: e.reportSupported ? "جاهز لرسالة تقرير الطالب" : e.reportReason
							})
						]
					}, e.id)) : /* @__PURE__ */ (0, M.jsx)("p", {
						className: "empty",
						children: "احفظ الحساب ثم زامن القوالب لتظهر هنا."
					})
				})
			]
		}),
		n.verifyToken && /* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel whatsapp-settings",
			children: [
				/* @__PURE__ */ (0, M.jsx)("h2", { children: "ربط الويب هوك" }),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "الويب هوك يحتاج رابط HTTPS عامًا على المنصة. هذا الرمز لتأكيد الرابط في Meta؛ إعدادات الإرسال تعمل من البرنامج."
				}),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["Verify Token", /* @__PURE__ */ (0, M.jsx)("input", {
					readOnly: !0,
					dir: "ltr",
					value: n.verifyToken
				})] }),
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					onClick: () => O(n.verifyToken),
					children: "نسخ رمز التحقق"
				})
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel whatsapp-settings",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "سجل إرسال التقارير" }), /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "«قبل واتساب الرسالة» تأكيد قبول الطلب، ويختلف عن وصولها أو قراءتها. الحالة غير المؤكدة لا يعاد إرسالها تلقائيًا."
				})] }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "text-button",
					disabled: m,
					onClick: () => D(S),
					children: "تحديث السجل"
				})]
			}), /* @__PURE__ */ (0, M.jsx)("div", {
				className: "table-scroll",
				children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الطالب" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "التقرير" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الحالة" })
				] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: o.length ? o.map((e, t) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: e.name }), /* @__PURE__ */ (0, M.jsxs)("small", {
						dir: "ltr",
						children: [
							e.code,
							" · ",
							e.phone
						]
					})] }),
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [e.filename, /* @__PURE__ */ (0, M.jsx)("small", {
						dir: "ltr",
						children: e.template
					})] }),
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [Pe[e.state] || e.state, /* @__PURE__ */ (0, M.jsx)("small", { children: e.error })] })
				] }, t)) : /* @__PURE__ */ (0, M.jsx)("tr", { children: /* @__PURE__ */ (0, M.jsx)("td", {
					colSpan: 3,
					className: "empty",
					children: "لم تُرسل تقارير بعد."
				}) }) })] })
			})]
		})
	] }) : /* @__PURE__ */ (0, M.jsx)("div", {
		className: "loading",
		children: "جارٍ قراءة إعدادات واتساب…"
	});
}
//#endregion
//#region src/SendReports.jsx
function Re({ attempts: e, label: t = "إرسال عبر واتساب", centerBatchId: n = "", recipientKind: r = "student", reportKind: i = "exam" }) {
	let a = !!n, [o, s] = (0, _.useState)(null), [c, l] = (0, _.useState)(""), [u, d] = (0, _.useState)([]), [f, p] = (0, _.useState)(null), [m, h] = (0, _.useState)(!1), [g, v] = (0, _.useState)(""), [y, x] = (0, _.useState)(""), S = a ? e : e.filter((e) => !e.cancelled_at && e.submitted_at && e.pending === 0), C = JSON.stringify([
		n,
		r,
		i,
		S.map((e) => e.id)
	]), w = o?.templates.find((e) => e.id === c && e.reportSupported), D = JSON.stringify([
		c,
		w?.bindingSchema,
		r,
		i
	]), O = ke(w, r, i), k = !!(O && JSON.stringify(O.mappings) === JSON.stringify(u)), ee = !!(w && Ae(w, u)), A = JSON.stringify([
		C,
		c,
		w?.bindingSchema,
		u
	]), ne = (0, _.useRef)(A);
	ne.current = A;
	let [re, j] = (0, _.useState)(""), ie = f && re === A, ae = async () => {
		x("");
		try {
			let [e, t, n] = await Promise.all([
				E("/api/bootstrap"),
				E("/api/whatsapp/configuration"),
				E("/api/whatsapp/templates", void 0, void 0, { timeoutMs: 12e4 })
			]);
			s({
				token: e.token,
				config: t,
				templates: n.templates,
				syncError: n.syncError
			}), p(null), v("");
		} catch (e) {
			x(e.message), T(e.message, !0);
		}
	};
	(0, _.useEffect)(() => {
		a && ae();
	}, [n]), (0, _.useEffect)(() => {
		d(je(w, r, i)), p(null);
	}, [D]), (0, _.useEffect)(() => {
		p(null);
	}, [C]), (0, _.useEffect)(() => {
		if (!o || m) return;
		let e = !0, t = setInterval(() => {
			E("/api/whatsapp/templates", void 0, void 0, { timeoutMs: 12e4 }).then((t) => {
				e && s((e) => e && {
					...e,
					templates: t.templates,
					syncError: t.syncError
				});
			}).catch((t) => {
				e && v(t.message);
			});
		}, 6e4);
		return () => {
			e = !1, clearInterval(t);
		};
	}, [!!o, m]);
	let oe = (e) => {
		l(e), p(null), v("");
	}, se = (e) => {
		d(e), p(null);
	}, ce = async () => {
		h(!0);
		try {
			let e = await E("/api/whatsapp/template-bindings", {
				templateId: c,
				bindingSchema: w.bindingSchema,
				recipientKind: r,
				reportKind: i,
				mappings: u
			}, o.token);
			s((t) => ({
				...t,
				templates: t.templates.map((t) => t.id === c ? {
					...t,
					savedBindings: [...(t.savedBindings || []).filter((e) => e.recipientKind !== r || e.reportKind !== i), e]
				} : t)
			})), v("تم تثبيت الربط لهذا القالب ونوع الرسالة والمستلم. تُملأ بيانات كل طالب تلقائيًا في المرات القادمة.");
		} catch (e) {
			T(e.message, !0);
		} finally {
			h(!1);
		}
	}, le = async () => {
		let e = A;
		h(!0);
		try {
			let t = await E("/api/whatsapp/reports/preview", {
				attemptIds: S.map((e) => e.id),
				templateId: c,
				bindingSchema: w.bindingSchema,
				mappings: u,
				centerBatchId: n,
				recipientKind: r
			}, o.token, { timeoutMs: 12e4 });
			ne.current === e && (p(t.items), j(e));
		} catch (e) {
			T(e.message, !0);
		} finally {
			h(!1);
		}
	}, ue = async (e = !1) => {
		if (!ie) return;
		let t = f.filter((t) => !t.error && (e ? ["failed", "rejected"].includes(t.prior?.state) : !t.prior));
		if (t.length && await te(`إرسال ${b(t.length)} رسالة على واتساب؟`, "راجع المعاينة والأرقام. كل مستلم تصله بيانات الطالب الظاهرة فقط.") && ne.current === A) {
			h(!0);
			try {
				await E("/api/whatsapp/jobs", {
					items: t.map((e) => ({
						attemptId: e.attemptId,
						fingerprint: e.fingerprint
					})),
					templateId: c,
					bindingSchema: w.bindingSchema,
					mappings: u,
					retry: e,
					centerBatchId: n,
					recipientKind: r
				}, o.token, { timeoutMs: 12e4 }), a || s(null), p(null), window.dispatchEvent(new Event("whatsapp-jobs-changed")), v("أُضيفت الرسائل لدور الإرسال. تابع حالتها في سجل الإرسال."), T("أُضيفت الرسائل لدور الإرسال.");
			} catch (e) {
				p(null), v("أعد المعاينة لقراءة السجل قبل الاستكمال."), T(e.message, !0);
			} finally {
				h(!1);
			}
		}
	};
	if (a && !o) return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel padded",
		children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "قالب الرسالة ومعاينتها" }), y ? /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("p", {
			className: "notice",
			role: "alert",
			children: y
		}), /* @__PURE__ */ (0, M.jsx)("button", {
			className: "button secondary",
			onClick: ae,
			children: "إعادة تحميل القوالب"
		})] }) : /* @__PURE__ */ (0, M.jsx)("p", {
			className: "loading",
			children: "جارٍ تحميل قوالب الحساب…"
		})]
	});
	let de = o && /* @__PURE__ */ (0, M.jsxs)("section", {
		className: `panel ${a ? "padded" : "send-dialog"}`,
		...a ? {} : {
			role: "dialog",
			"aria-modal": !0,
			"aria-label": "إرسال تقارير واتساب"
		},
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "قالب الرسالة ومعاينتها" }), !a && /* @__PURE__ */ (0, M.jsx)("button", {
					className: "text-button",
					disabled: m,
					onClick: () => s(null),
					children: "إغلاق"
				})]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", { children: a ? `${b(S.length)} طالب محدد برقم صالح` : `${b(S.length)} تقرير مكتمل التصحيح · ${b(e.length - S.length)} طالب لم يسلّم أو ينتظر التصحيح أو محاولته ملغاة.` }),
			o.config.configured ? /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
				o.syncError && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "notice",
					children: o.syncError
				}),
				/* @__PURE__ */ (0, M.jsx)(Ie, {
					templates: o.templates,
					templateId: c,
					setTemplateId: oe,
					mappings: u,
					setMappings: se,
					disabled: m,
					recipientKind: r,
					reportKind: i
				}),
				w && /* @__PURE__ */ (0, M.jsxs)("div", {
					className: "actions",
					children: [/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: m || !ee || k,
						onClick: ce,
						children: k ? "ربط المتغيرات مثبت" : "تثبيت ربط المتغيرات"
					}), /* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "اختار مصدر كل متغير مرة واحدة. تُقرأ الدرجة والنهاية العظمى من سجل الطالب، ومنها الورقي من ١٠ والإلكتروني من ١٢. نص القالب الثابت يظل كما اعتمدته Meta؛ اختار قالب الحضور أو الغياب المناسب."
					})]
				}),
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "actions",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("button", {
							className: "button secondary",
							disabled: m || !S.length || !ee,
							onClick: le,
							children: [
								"معاينة الرسائل (",
								b(S.length),
								")"
							]
						}),
						ie && /* @__PURE__ */ (0, M.jsxs)("button", {
							className: "button",
							disabled: m || !f.some((e) => !e.error && !e.prior),
							onClick: () => ue(),
							children: [
								"إرسال الرسائل الجديدة (",
								b(f.filter((e) => !e.error && !e.prior).length),
								")"
							]
						}),
						ie && f.some((e) => ["failed", "rejected"].includes(e.prior?.state)) && /* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary",
							disabled: m,
							onClick: () => ue(!0),
							children: "إعادة محاولة الرسائل التي لم تُرسل"
						})
					]
				})
			] }) : /* @__PURE__ */ (0, M.jsx)("div", {
				className: "notice",
				children: "افتح «إعداد حساب واتساب والقوالب» لربط الحساب أول مرة."
			}),
			g && /* @__PURE__ */ (0, M.jsx)("p", {
				className: "notice",
				role: "status",
				children: g
			}),
			ie && f.some((e) => e.error) && /* @__PURE__ */ (0, M.jsxs)("p", {
				className: "notice",
				role: "alert",
				children: [b(f.filter((e) => e.error).length), " طالب لن يدخل الإرسال لأن القالب لا يطابق حالته. غيّر القالب أو تحديد الطلاب ثم أعد المعاينة."]
			}),
			ie && /* @__PURE__ */ (0, M.jsx)("div", {
				className: "table-scroll",
				children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الطالب والرقم" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "معاينة الرسالة" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "حالة الإرسال" })
				] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: f.map((e) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [
						/* @__PURE__ */ (0, M.jsx)("strong", { children: e.name }),
						/* @__PURE__ */ (0, M.jsx)("small", {
							dir: "ltr",
							children: e.phone
						}),
						/* @__PURE__ */ (0, M.jsxs)("small", { children: [
							"الكود: ",
							e.code,
							" · ",
							e.score == null ? "لم تُرصد" : b(e.score),
							" من ",
							e.maximum == null ? "غير معروفة" : b(e.maximum)
						] })
					] }),
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [/* @__PURE__ */ (0, M.jsx)("small", {
						className: "whatsapp-message",
						dir: "auto",
						children: e.error ? "لن تُرسل لهذا الطالب" : Ne(w, e)
					}), !a && /* @__PURE__ */ (0, M.jsx)("small", { children: e.filename })] }),
					/* @__PURE__ */ (0, M.jsxs)("td", { children: [e.error ? "مستبعد من الإرسال" : e.prior ? Pe[e.prior.state] : "جاهز للإرسال", /* @__PURE__ */ (0, M.jsx)("small", { children: e.error || e.prior?.error })] })
				] }, e.attemptId)) })] })
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الإرسال حتى ٥ رسائل معًا، والمحاولة تتكرر للأخطاء المؤقتة المؤكدة. الرسائل المقبولة أو غير المؤكدة محفوظة لمنع تكرارها. أبقِ البرنامج مفتوحًا ومتصلًا بالإنترنت."
			})
		]
	});
	return a ? de : /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("button", {
		className: "button secondary small",
		onClick: ae,
		children: t
	}), o && /* @__PURE__ */ (0, M.jsx)("div", {
		className: "send-overlay",
		children: de
	})] });
}
//#endregion
//#region src/WhatsAppJobs.jsx
var ze = {
	queued: "في دور الإرسال",
	running: "جارٍ الإرسال",
	completed: "اكتمل الإرسال",
	partial: "توقف ويحتاج متابعة",
	stopped: "تم الإيقاف",
	interrupted: "توقف عند إغلاق البرنامج"
};
function Be() {
	let [e, t] = (0, _.useState)([]), [n, r] = (0, _.useState)(""), [i, a] = (0, _.useState)(""), o = async () => {
		let e = await E("/api/whatsapp/jobs");
		t(e.items);
	};
	(0, _.useEffect)(() => {
		let e = !0, n = !1, i = async () => {
			if (!n) {
				n = !0;
				try {
					let n = await E("/api/whatsapp/jobs");
					e && t(n.items);
				} catch {} finally {
					n = !1;
				}
			}
		};
		E("/api/bootstrap").then((t) => {
			e && r(t.token);
		}).catch(() => {}), i();
		let a = setInterval(i, 3e3);
		return window.addEventListener("whatsapp-jobs-changed", i), () => {
			e = !1, clearInterval(a), window.removeEventListener("whatsapp-jobs-changed", i);
		};
	}, []);
	let s = async (e) => {
		a(e);
		try {
			await E("/api/whatsapp/jobs/resume", { jobId: e }, n), await o();
		} catch (e) {
			T(e.message, !0);
		} finally {
			a("");
		}
	}, c = async (e) => {
		a(e);
		try {
			await E("/api/whatsapp/jobs/stop", { jobId: e }, n), await o();
		} catch (e) {
			T(e.message, !0);
		} finally {
			a("");
		}
	};
	if (!e.length) return null;
	let l = e.filter((e) => ["queued", "running"].includes(e.state)), u = [...l, ...e.filter((e) => !l.includes(e)).slice(0, 2)];
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel",
		"aria-label": "إرسال واتساب في الخلفية",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("strong", { children: [
					"إرسال واتساب في الخلفية · ",
					b(l.length),
					" عملية نشطة"
				] }), /* @__PURE__ */ (0, M.jsx)("span", {
					className: "help",
					children: "يمكنك متابعة العمل في أي امتحان"
				})]
			}),
			u.map((e) => /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "notice",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "section-top",
						children: [
							/* @__PURE__ */ (0, M.jsxs)("strong", { children: [
								e.title,
								" · ",
								e.stopping && e.state === "running" ? "يتوقف بعد الدفعة الحالية" : ze[e.state]
							] }),
							["queued", "running"].includes(e.state) && /* @__PURE__ */ (0, M.jsx)("button", {
								className: "text-button",
								disabled: !n || i === e.id || e.stopping,
								onClick: () => c(e.id),
								children: e.state === "queued" ? "إلغاء من الدور" : "إيقاف بعد الدفعة الحالية"
							}),
							[
								"partial",
								"stopped",
								"interrupted"
							].includes(e.state) && /* @__PURE__ */ (0, M.jsx)("button", {
								className: "text-button",
								disabled: !n || i === e.id,
								onClick: () => s(e.id),
								children: "استكمال الباقي بدون تكرار المقبول"
							})
						]
					}),
					/* @__PURE__ */ (0, M.jsxs)("p", { children: [
						"تمت معالجة ",
						b(e.processed),
						" من ",
						b(e.total),
						" · قبل واتساب ",
						b(e.accepted),
						" · لم يتأكد إرسال ",
						b(e.failed)
					] }),
					e.message && /* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: e.message
					})
				]
			}, e.id)),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "٥ تقارير معًا، ثم ٥ ثوانٍ بين الدفعات. لو انقطع الإنترنت ينتظر ويستكمل تلقائيًا. الرسائل المقبولة لا تُرسل مرة أخرى، والرسالة غير المؤكدة تحتاج متابعة. أبقِ البرنامج مفتوحًا. قبول الطلب يختلف عن وصول الرسالة."
			})
		]
	});
}
//#endregion
//#region src/CenterWhatsAppReports.jsx
var Ve = {
	followup: "متابعة الحضور والواجب والدرجة",
	attendance: "الحضور والغياب",
	homework: "عمل الواجب أو لم يعمل",
	grade: "درجة الطالب"
};
function He(e) {
	let t = String(e || "").replace(/[٠-٩۰-۹]/g, (e) => String(e.charCodeAt(0) - (e <= "٩" ? 1632 : 1776))).replace(/[\s()-]/g, "");
	return t.startsWith("0020") ? t = "0" + t.slice(4) : t.startsWith("+20") ? t = "0" + t.slice(3) : t.startsWith("20") && t.length === 12 && (t = "0" + t.slice(2)), /^01[0125]\d{8}$/.test(t);
}
function Ue({ batchId: e }) {
	let [t, n] = (0, _.useState)(null), [r, i] = (0, _.useState)(""), [a, o] = (0, _.useState)(/* @__PURE__ */ new Set()), [s, c] = (0, _.useState)("guardian");
	if ((0, _.useEffect)(() => {
		let t = !0;
		return n(null), i(""), E(`/api/whatsapp/center/batches/${encodeURIComponent(e)}`).then((e) => {
			t && (n(e), o(new Set(e.records.map((e) => e.id))));
		}).catch((e) => {
			t && i(e.message);
		}), () => {
			t = !1;
		};
	}, [e]), r) return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel padded",
		children: [/* @__PURE__ */ (0, M.jsx)("h1", { children: "تعذر فتح رسائل السنتر" }), /* @__PURE__ */ (0, M.jsx)("p", {
			role: "alert",
			children: r
		})]
	});
	if (!t) return /* @__PURE__ */ (0, M.jsx)("p", {
		className: "loading",
		children: "جارٍ تحميل الطلاب وبيانات الرسائل…"
	});
	let l = (e) => s === "guardian" ? e.guardianPhone : e.phone, u = t.records.filter((e) => a.has(e.id)), d = u.filter((e) => He(l(e))), f = t.records.length > 0 && t.records.every((e) => a.has(e.id)), p = t.messageKind || "followup";
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsxs)("header", {
			className: "page-heading",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h1", { children: "رسائل واتساب" }), /* @__PURE__ */ (0, M.jsxs)("p", { children: [
				t.title,
				" · ",
				Ve[p] || Ve.followup
			] })] }), /* @__PURE__ */ (0, M.jsx)("a", {
				className: "button secondary",
				href: Me("whatsapp", e),
				children: "إعداد حساب واتساب والقوالب"
			})]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel padded",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["المستلم", /* @__PURE__ */ (0, M.jsxs)("select", {
					value: s,
					onChange: (e) => c(e.target.value),
					children: [/* @__PURE__ */ (0, M.jsx)("option", {
						value: "guardian",
						children: "ولي الأمر"
					}), /* @__PURE__ */ (0, M.jsx)("option", {
						value: "student",
						children: "الطالب"
					})]
				})] }), /* @__PURE__ */ (0, M.jsxs)("p", {
					role: "status",
					children: [
						b(d.length),
						" رسالة برقم صالح · ",
						b(u.length - d.length),
						" رقم مفقود أو غير صالح"
					]
				})]
			}), /* @__PURE__ */ (0, M.jsxs)("details", { children: [
				/* @__PURE__ */ (0, M.jsxs)("summary", { children: [
					"مراجعة الطلاب المحددين (",
					b(u.length),
					")"
				] }),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: [/* @__PURE__ */ (0, M.jsx)("input", {
					type: "checkbox",
					checked: f,
					onChange: (e) => o(e.target.checked ? new Set(t.records.map((e) => e.id)) : /* @__PURE__ */ new Set())
				}), " تحديد الكل"] }),
				/* @__PURE__ */ (0, M.jsx)("div", {
					className: "table-scroll",
					children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
						/* @__PURE__ */ (0, M.jsx)("th", { children: "اختيار" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الطالب والكود" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الرقم" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "المجموعة والحصة" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الحضور" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الواجب" }),
						/* @__PURE__ */ (0, M.jsx)("th", { children: "الدرجة" })
					] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: t.records.map((e) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
						/* @__PURE__ */ (0, M.jsx)("td", { children: /* @__PURE__ */ (0, M.jsx)("input", {
							"aria-label": `اختيار ${e.name}`,
							type: "checkbox",
							checked: a.has(e.id),
							onChange: (t) => o((n) => {
								let r = new Set(n);
								return t.target.checked ? r.add(e.id) : r.delete(e.id), r;
							})
						}) }),
						/* @__PURE__ */ (0, M.jsxs)("td", { children: [e.name, /* @__PURE__ */ (0, M.jsx)("small", { children: e.code })] }),
						/* @__PURE__ */ (0, M.jsxs)("td", {
							dir: "ltr",
							children: [l(e) || "غير مسجل", /* @__PURE__ */ (0, M.jsx)("small", { children: He(l(e)) ? "" : "لن يدخل الإرسال" })]
						}),
						/* @__PURE__ */ (0, M.jsxs)("td", { children: [e.group, /* @__PURE__ */ (0, M.jsxs)("small", { children: [
							e.lesson,
							" · ",
							e.exam
						] })] }),
						/* @__PURE__ */ (0, M.jsx)("td", { children: e.attendance || "غير مسجل" }),
						/* @__PURE__ */ (0, M.jsx)("td", { children: e.homework || "غير مسجل" }),
						/* @__PURE__ */ (0, M.jsx)("td", { children: e.score == null ? "لم تُرصد" : `${e.score} من ${e.maximum ?? "غير معروفة"}` })
					] }, e.id)) })] })
				})
			] })]
		}),
		/* @__PURE__ */ (0, M.jsx)(Re, {
			attempts: d,
			centerBatchId: e,
			recipientKind: s,
			reportKind: p
		}),
		/* @__PURE__ */ (0, M.jsx)(Be, {})
	] });
}
//#endregion
//#region src/CenterSetup.jsx
var We = [
	{
		kind: "grades",
		title: "الصفوف الدراسية",
		singular: "الصف الدراسي",
		parent: null
	},
	{
		kind: "centers",
		title: "السناتر",
		singular: "السنتر",
		parent: "grades",
		key: "gradeId"
	},
	{
		kind: "groups",
		title: "المجموعات",
		singular: "المجموعة",
		parent: "centers",
		key: "centerId"
	},
	{
		kind: "lessons",
		title: "الحصص",
		singular: "الحصة",
		parent: "groups",
		key: "groupId"
	}
];
function Ge({ catalog: e, request: t, refresh: n }) {
	let [r, i] = (0, _.useState)({}), [a, o] = (0, _.useState)({}), [s, c] = (0, _.useState)(!1), l = (e, t) => i((n) => {
		let r = {
			...n,
			[e]: t
		}, i = We.findIndex((t) => t.kind === e);
		return We.slice(i + 1).forEach((e) => delete r[e.kind]), r;
	}), u = async (e, i) => {
		i.preventDefault();
		let s = (a[e.kind] || "").trim();
		if (s) {
			c(!0);
			try {
				let i = { name: s };
				e.parent && (i[e.key] = r[e.parent]);
				let a = await t(`/api/catalog/${e.kind}`, i);
				await n(), o((t) => ({
					...t,
					[e.kind]: ""
				})), l(e.kind, a.id), T(`تمت إضافة ${e.singular}`);
			} catch (e) {
				T(e.message, !0);
			} finally {
				c(!1);
			}
		}
	}, d = async (e, r) => {
		let i = window.prompt(`الاسم الجديد لـ${e.singular}`, r.name)?.trim();
		if (i && i !== r.name) {
			c(!0);
			try {
				await t(`/api/catalog/${e.kind}/${r.id}/rename`, { name: i }), await n(), T("تم تعديل الاسم");
			} catch (e) {
				T(e.message, !0);
			} finally {
				c(!1);
			}
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("div", {
		className: "page-heading",
		children: /* @__PURE__ */ (0, M.jsxs)("div", { children: [
			/* @__PURE__ */ (0, M.jsx)("span", {
				className: "context-label",
				children: "تنظيم السنتر"
			}),
			/* @__PURE__ */ (0, M.jsx)("h1", { children: "الصفوف والسناتر والمجموعات والحصص" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: "رتّب أماكن التدريس مرة واحدة. في قاعة السنتر تختار الحصة ثم اسم الامتحان المناسب لها." })
		] })
	}), /* @__PURE__ */ (0, M.jsx)("div", {
		className: "catalog-grid",
		children: We.map((t) => {
			let n = t.parent && r[t.parent], i = (e?.[t.kind] || []).filter((e) => !t.parent || e[t.key] === n), c = !t.parent || !!n;
			return /* @__PURE__ */ (0, M.jsxs)("section", {
				className: "panel catalog-column",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "catalog-heading",
					children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: t.title }), /* @__PURE__ */ (0, M.jsx)("span", {
						className: "badge quiet",
						children: b(i.length)
					})]
				}), c ? /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("div", {
					className: "catalog-items",
					children: i.length ? i.map((e) => /* @__PURE__ */ (0, M.jsxs)("div", {
						className: `catalog-item ${r[t.kind] === e.id ? "selected" : ""}`,
						children: [/* @__PURE__ */ (0, M.jsx)("button", {
							type: "button",
							onClick: () => l(t.kind, e.id),
							children: e.name
						}), /* @__PURE__ */ (0, M.jsx)("button", {
							className: "catalog-rename",
							type: "button",
							"aria-label": `تعديل ${e.name}`,
							onClick: () => d(t, e),
							children: "تعديل"
						})]
					}, e.id)) : /* @__PURE__ */ (0, M.jsxs)("p", {
						className: "help catalog-empty",
						children: [
							"أضف أول ",
							t.singular,
							" هنا."
						]
					})
				}), /* @__PURE__ */ (0, M.jsxs)("form", {
					className: "catalog-add",
					onSubmit: (e) => u(t, e),
					children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: [
						"إضافة ",
						t.singular,
						/* @__PURE__ */ (0, M.jsx)("input", {
							required: !0,
							maxLength: "150",
							value: a[t.kind] || "",
							onChange: (e) => o((n) => ({
								...n,
								[t.kind]: e.target.value
							})),
							placeholder: `اسم ${t.singular}`
						})
					] }), /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary small",
						disabled: s,
						children: "＋ إضافة"
					})]
				})] }) : /* @__PURE__ */ (0, M.jsxs)("p", {
					className: "help catalog-empty",
					children: [
						"اختر ",
						We.find((e) => e.kind === t.parent)?.singular,
						" أولًا."
					]
				})]
			}, t.kind);
		})
	})] });
}
//#endregion
//#region src/DownloadReports.jsx
function Ke({ examId: e, attempts: t }) {
	let [n, r] = (0, _.useState)(!1), i = t.filter((e) => !e.cancelled_at && e.submitted_at && e.pending === 0).length;
	return /* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("button", {
		className: "button secondary small",
		disabled: n || !i,
		onClick: async () => {
			if (!n) {
				r(!0);
				try {
					await A(`/api/exams/${e}/reports.zip`, "تقارير-الطلاب.zip", "application/zip") && T("تم تجهيز ملف تقارير الطلاب");
				} catch (e) {
					T(e.message, !0);
				} finally {
					r(!1);
				}
			}
		},
		children: n ? "جارٍ تجهيز ملفات PDF…" : `تنزيل تقارير PDF في ZIP (${b(i)})`
	}), /* @__PURE__ */ (0, M.jsx)("p", {
		className: "help",
		children: "يشمل التقارير مكتملة التصحيح فقط. اسم الملف: اسم الطالب — درجته من المجموع."
	})] });
}
//#endregion
//#region src/Sheets.jsx
function qe({ catalog: e, exams: t }) {
	let [n, r] = (0, _.useState)(!1), i = async () => {
		r(!0);
		try {
			await A(`/api/exams/${s}/results.xlsx?groupId=${encodeURIComponent(a)}`, `نتائج-${e.groups.find((e) => e.id === a)?.name || "المجموعة"}.xlsx`, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") && T("تم تنزيل شيت Excel");
		} catch (e) {
			T(e.message, !0);
		} finally {
			r(!1);
		}
	}, [a, o] = (0, _.useState)(""), [s, c] = (0, _.useState)(""), [l, u] = (0, _.useState)(null), [d, f] = (0, _.useState)(""), p = (t) => {
		let n = e.centers.find((e) => e.id === t.centerId);
		return [
			e.grades.find((e) => e.id === n?.gradeId)?.name,
			n?.name,
			t.name
		].filter(Boolean).join(" · ");
	}, m = t.filter((t) => e.lessons.some((e) => e.id === t.lessonId && e.groupId === a));
	(0, _.useEffect)(() => {
		let e = !0;
		return u(null), f(""), s && E(`/api/exams/${s}`).then((t) => {
			e && u(t);
		}).catch((t) => {
			e && f(t.message);
		}), () => {
			e = !1;
		};
	}, [s]);
	let h = l?.attempts.filter((e) => e.joined_at) || [];
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsx)("div", {
			className: "page-heading",
			children: /* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: "نتائج المجموعات"
				}),
				/* @__PURE__ */ (0, M.jsx)("h1", { children: "شيتات" }),
				/* @__PURE__ */ (0, M.jsx)("p", { children: "اختر المجموعة وجلسة الامتحان لتنزيل النتائج في ملف Excel." })
			] })
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel padded",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "launcher-grid",
					children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["المجموعة", /* @__PURE__ */ (0, M.jsxs)("select", {
						value: a,
						onChange: (e) => {
							o(e.target.value), c("");
						},
						children: [/* @__PURE__ */ (0, M.jsx)("option", {
							value: "",
							children: "اختر المجموعة"
						}), e.groups.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
							value: e.id,
							children: p(e)
						}, e.id))]
					})] }), /* @__PURE__ */ (0, M.jsxs)("label", { children: ["الامتحان", /* @__PURE__ */ (0, M.jsxs)("select", {
						disabled: !a,
						value: s,
						onChange: (e) => c(e.target.value),
						children: [/* @__PURE__ */ (0, M.jsx)("option", {
							value: "",
							children: "اختر جلسة الامتحان"
						}), m.map((t) => /* @__PURE__ */ (0, M.jsxs)("option", {
							value: t.id,
							children: [
								t.title,
								" · ",
								e.lessons.find((e) => e.id === t.lessonId)?.name,
								" · ",
								(/* @__PURE__ */ new Date(t.createdAt * 1e3)).toLocaleString("ar-EG")
							]
						}, t.id))]
					})] })]
				}),
				a && !m.length && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "لا توجد جلسات امتحان مرتبطة بهذه المجموعة."
				}),
				d && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "inline-error",
					children: d
				}),
				s && !l && !d && /* @__PURE__ */ (0, M.jsx)("p", {
					role: "status",
					children: "جارٍ قراءة النتائج…"
				}),
				l && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "actions",
					children: [
						/* @__PURE__ */ (0, M.jsx)("button", {
							className: "button",
							disabled: n,
							onClick: i,
							children: n ? "جارٍ تجهيز الشيت…" : "تنزيل شيت Excel ↓"
						}),
						/* @__PURE__ */ (0, M.jsx)(Ke, {
							examId: s,
							attempts: h
						}),
						/* @__PURE__ */ (0, M.jsx)(Re, {
							attempts: h,
							label: "إرسال تقارير المجموعة عبر واتساب"
						})
					]
				}), /* @__PURE__ */ (0, M.jsxs)("p", {
					className: "help",
					children: [b(h.length), " طالب حضر. العناوين في أول صف، وبيانات الطلاب من الصف الثاني. النتيجة النهائية تظهر بعد اكتمال التصحيح فقط؛ الموبايل والكود محفوظان كنص للحفاظ على الصفر الأول."]
				})] })
			]
		}),
		l && /* @__PURE__ */ (0, M.jsx)("section", {
			className: "panel table-scroll",
			children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
				/* @__PURE__ */ (0, M.jsx)("th", { children: "الطالب" }),
				/* @__PURE__ */ (0, M.jsx)("th", { children: "الموبايل" }),
				/* @__PURE__ */ (0, M.jsx)("th", { children: "الدرجة" }),
				/* @__PURE__ */ (0, M.jsx)("th", { children: "الحالة" }),
				/* @__PURE__ */ (0, M.jsx)("th", { children: "سبب الإلغاء" }),
				/* @__PURE__ */ (0, M.jsx)("th", { children: "التقرير" })
			] }) }), /* @__PURE__ */ (0, M.jsxs)("tbody", { children: [h.map((e) => /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
				/* @__PURE__ */ (0, M.jsx)("td", { children: e.name }),
				/* @__PURE__ */ (0, M.jsx)("td", {
					dir: "ltr",
					children: e.phone
				}),
				/* @__PURE__ */ (0, M.jsx)("td", { children: !e.cancelled_at && e.submitted_at && e.pending === 0 ? `${b(e.score)} من ${b(e.maximum)} (${b(e.maximum ? 100 * e.score / e.maximum : 0)}٪)` : "—" }),
				/* @__PURE__ */ (0, M.jsx)("td", { children: e.cancelled_at ? "ملغي" : e.submitted_at ? e.pending ? `متبقي ${b(e.pending)} للتصحيح` : "تم التصحيح" : "لم يسلّم" }),
				/* @__PURE__ */ (0, M.jsx)("td", {
					className: "cancellation-reason",
					children: e.cancelReason || "—"
				}),
				/* @__PURE__ */ (0, M.jsx)("td", { children: !e.cancelled_at && e.submitted_at && e.pending === 0 ? /* @__PURE__ */ (0, M.jsx)("button", {
					className: "text-button",
					onClick: () => A(`/api/attempts/${e.id}/report.pdf`, `تقرير-${e.name}.pdf`, "application/pdf").catch((e) => T(e.message, !0)),
					children: "تنزيل PDF"
				}) : "التقرير غير جاهز" })
			] }, e.id)), !h.length && /* @__PURE__ */ (0, M.jsx)("tr", { children: /* @__PURE__ */ (0, M.jsx)("td", {
				colSpan: "6",
				children: "لم يدخل طلاب هذه الجلسة."
			}) })] })] })
		})
	] });
}
//#endregion
//#region src/LiveTimer.jsx
function Je({ dashboard: e, request: t, refresh: n }) {
	let [r, i] = (0, _.useState)(Date.now()), [a, o] = (0, _.useState)(5), [s, c] = (0, _.useState)(!1);
	(0, _.useEffect)(() => {
		let e = setInterval(() => i(Date.now()), 1e3);
		return () => clearInterval(e);
	}, []);
	let l = e.exam, [u, d] = (0, _.useState)({
		time: e.serverTime,
		local: Date.now()
	});
	(0, _.useEffect)(() => d({
		time: e.serverTime,
		local: Date.now()
	}), [e.serverTime]);
	let f = u.time + (r - u.local) / 1e3, p = e.attempts.filter((e) => e.joined_at && !e.cancelled_at && !e.submitted_at && e.deadline > f).map((e) => e.deadline), m = Math.max(l.config.timerMode === "shared" ? l.started_at + l.config.minutes * 60 + (l.config.extraSeconds || 0) : 0, ...p), h = Math.max(0, Math.ceil(m - f)), g = async (e) => {
		if (!s) {
			c(!0);
			try {
				await t(`/api/exams/${l.id}/extend-time`, { minutes: e }), await n(), T(`تمت إضافة ${b(e)} دقيقة للوقت`);
			} catch (e) {
				T(e.message, !0);
			} finally {
				c(!1);
			}
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel live-timer",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("span", { children: l.config.timerMode === "shared" ? "أطول وقت متبقٍ (يشمل تمديد الطلاب)" : "أطول وقت متبقٍ لطالب" }), /* @__PURE__ */ (0, M.jsxs)("strong", {
				dir: "ltr",
				children: [
					String(Math.floor(h / 60)).padStart(2, "0"),
					":",
					String(h % 60).padStart(2, "0")
				]
			})] }),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "actions",
				children: [
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: s,
						onClick: () => g(2),
						children: "＋ دقيقتين للجميع"
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary",
						disabled: s,
						onClick: () => g(5),
						children: "＋ ٥ دقائق للجميع"
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", { children: ["دقائق إضافية", /* @__PURE__ */ (0, M.jsx)("input", {
						type: "number",
						min: "1",
						max: "120",
						step: "1",
						value: a,
						onChange: (e) => o(Number(e.target.value))
					})] }),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						disabled: s || !Number.isInteger(a) || a < 1 || a > 120,
						onClick: () => g(a),
						children: "إضافة الوقت للجميع"
					})
				]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الزيادة تشمل الطلاب الذين ما زالوا يحلون والدخول المتأخر. لا تفتح محاولة سبق تسليمها."
			})
		]
	});
}
//#endregion
//#region src/rosterStatus.js
var Ye = [
	["all", "الكل"],
	["solving", "يحل الآن"],
	["waiting", "في الانتظار"],
	["away", "خارج صفحة الامتحان"],
	["disconnected", "غير متصل"],
	["paused", "موقوف مؤقتًا"],
	["active", "لم يسلّم"],
	["submitted", "تم التسليم"],
	["expired", "انتهى وقته"],
	["pending", "بانتظار التصحيح"],
	["graded", "تم التصحيح"],
	["cancelled", "ملغي"]
], Xe = [
	["fullscreen", "ملء الشاشة"],
	["fullscreen_exit", "خارج ملء الشاشة"],
	["screen_reduced", "مساحة عرض أقل"],
	["device_changed", "علامة متصفح مختلفة"],
	["fullscreen_unsupported", "ملء الشاشة غير مدعوم"],
	["screen_missing", "بدون بيانات شاشة"]
];
function Ze(e, t, n) {
	return e.cancelled_at ? {
		key: "cancelled",
		tone: "disconnected",
		label: "ملغي"
	} : e.submitted_at ? e.submit_reason === "timeout" ? {
		key: "expired",
		tone: "complete",
		label: "انتهى وقته · تم التسليم"
	} : {
		key: "submitted",
		tone: "complete",
		label: "تم التسليم"
	} : e.deadline && e.deadline <= n ? {
		key: "expired",
		tone: "disconnected",
		label: "انتهى وقته"
	} : e.paused ? {
		key: "paused",
		tone: "disconnected",
		label: "موقوف مؤقتًا"
	} : !e.last_seen || n - e.last_seen >= 15 ? {
		key: "disconnected",
		tone: "disconnected",
		label: "غير متصل"
	} : t.state === "waiting" ? {
		key: "waiting",
		tone: "waiting",
		label: "في الانتظار"
	} : t.state === "running" && e.hiddenSince ? {
		key: "away",
		tone: "disconnected",
		label: "خارج صفحة الامتحان"
	} : t.state === "running" ? {
		key: "solving",
		tone: "waiting",
		label: "يحل الآن"
	} : {
		key: "active",
		tone: "quiet",
		label: "لم يسلّم"
	};
}
function Qe(e, t, n) {
	if (n === "all") return !0;
	if (Xe.some(([e]) => e === n)) {
		if (e.cancelled_at || e.submitted_at) return !1;
		let t = e.screen?.current;
		if (n === "screen_missing") return !t;
		if (!t) return !1;
		if (n === "fullscreen") return t.fullscreen === !0;
		if (n === "fullscreen_exit") return t.fullscreenSupported === !0 && t.fullscreen !== !0;
		if (n === "fullscreen_unsupported") return t.fullscreenSupported !== !0;
		if (n === "screen_reduced") return e.screen.issue?.startsWith("انخفض") === !0;
		if (n === "device_changed") return e.screen.issue === "تغيّرت علامة المتصفح";
	}
	return n === "active" ? !e.cancelled_at && !e.submitted_at : n === "submitted" ? !e.cancelled_at && !!e.submitted_at : n === "pending" ? !e.cancelled_at && !!e.submitted_at && e.pending > 0 : n === "graded" ? !e.cancelled_at && !!e.submitted_at && e.pending === 0 : t.key === n;
}
function $e(e, t, n) {
	if (!e.joined_at || e.cancelled_at || e.submitted_at || !["waiting", "running"].includes(t.state)) return "";
	let r = Ze(e, t, n);
	return [
		"paused",
		"away",
		"disconnected",
		"expired"
	].includes(r.key) ? r.label : t.state === "running" && t.config.screenGuard && e.screen?.issue || "";
}
//#endregion
//#region src/SelectedTime.jsx
function et({ examId: e, attempts: t, request: n, refresh: r, clear: i }) {
	let [a, o] = (0, _.useState)(5), [s, c] = (0, _.useState)(!1), l = async (a) => {
		if (!s && t.length) {
			c(!0);
			try {
				await n(`/api/exams/${e}/extend-time`, {
					minutes: a,
					attemptIds: t.map((e) => e.id)
				}), i(), await r(), T(`تمت إضافة ${b(a)} دقيقة لـ${b(t.length)} طالب فقط`);
			} catch (e) {
				T(e.message, !0);
			} finally {
				c(!1);
			}
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "selected-time",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "تمديد الوقت لطلاب معينين" }), /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "help",
			children: [
				"حدد الطلاب من المربعات في الجدول. المحدد الآن: ",
				b(t.length),
				". وقت باقي الطلاب لا يتغير."
			]
		})] }), /* @__PURE__ */ (0, M.jsxs)("div", {
			className: "actions",
			children: [
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary small",
					disabled: s || !t.length,
					onClick: () => l(2),
					children: "＋ دقيقتين للمحددين"
				}),
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary small",
					disabled: s || !t.length,
					onClick: () => l(5),
					children: "＋ ٥ دقائق للمحددين"
				}),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["دقائق للمحددين", /* @__PURE__ */ (0, M.jsx)("input", {
					type: "number",
					min: "1",
					max: "120",
					step: "1",
					value: a,
					onChange: (e) => o(Number(e.target.value))
				})] }),
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button small",
					disabled: s || !t.length || !Number.isInteger(a) || a < 1 || a > 120,
					onClick: () => l(a),
					children: "تمديد للمحددين"
				}),
				t.length > 0 && /* @__PURE__ */ (0, M.jsx)("button", {
					className: "text-button",
					disabled: s,
					onClick: i,
					children: "إلغاء التحديد"
				})
			]
		})]
	});
}
//#endregion
//#region src/CancelAttempt.jsx
function tt({ attempt: e, request: t, refresh: n, onClose: r }) {
	let [i, a] = (0, _.useState)(""), [o, s] = (0, _.useState)(!1), [c, l] = (0, _.useState)("");
	return /* @__PURE__ */ (0, M.jsx)("div", {
		className: "send-overlay",
		children: /* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel send-dialog",
			role: "dialog",
			"aria-modal": "true",
			"aria-labelledby": "cancel-attempt-title",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("h2", {
					id: "cancel-attempt-title",
					children: ["إلغاء محاولة ", e.name]
				}),
				/* @__PURE__ */ (0, M.jsxs)("p", { children: ["كود الطالب: ", /* @__PURE__ */ (0, M.jsx)("b", {
					dir: "ltr",
					children: e.code
				})] }),
				/* @__PURE__ */ (0, M.jsx)("p", {
					className: "notice",
					children: "يتوقف الطالب عن الحل ولا يمكنه العودة لهذه المحاولة. تظل إجاباته محفوظة للمراجعة، وتظهر حالته «ملغي» وسبب الإلغاء في الشيت."
				}),
				/* @__PURE__ */ (0, M.jsxs)("form", {
					onSubmit: async (a) => {
						if (a.preventDefault(), !o) {
							s(!0), l("");
							try {
								await t(`/api/attempts/${e.id}/cancel`, { reason: i.trim() }), await n(), r(), T("تم إلغاء المحاولة وحفظ السبب في الشيت");
							} catch (e) {
								l(e.message);
							} finally {
								s(!1);
							}
						}
					},
					children: [
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["سبب الإلغاء", /* @__PURE__ */ (0, M.jsx)("textarea", {
							autoFocus: !0,
							required: !0,
							maxLength: 1e3,
							rows: 4,
							value: i,
							onChange: (e) => a(e.target.value),
							placeholder: "اكتب سبب الإلغاء…"
						})] }),
						c && /* @__PURE__ */ (0, M.jsx)("p", {
							role: "alert",
							className: "inline-error",
							children: c
						}),
						/* @__PURE__ */ (0, M.jsxs)("div", {
							className: "actions",
							children: [/* @__PURE__ */ (0, M.jsx)("button", {
								className: "button danger",
								disabled: o || !i.trim(),
								children: o ? "جارٍ الإلغاء…" : "تأكيد إلغاء المحاولة"
							}), /* @__PURE__ */ (0, M.jsx)("button", {
								type: "button",
								className: "button secondary",
								disabled: o,
								onClick: r,
								children: "رجوع"
							})]
						})
					]
				})
			]
		})
	});
}
//#endregion
//#region src/rosterOrder.js
var nt = [
	["newest", "أحدث دخول أولًا"],
	["oldest", "أقدم دخول أولًا"],
	["seen", "أحدث ظهور أولًا"],
	["submitted", "أحدث تسليم أولًا"],
	["name", "الاسم من أ إلى ي"],
	["code", "الكود من الأصغر للأكبر"],
	["highest", "أعلى درجة بعد التصحيح"],
	["lowest", "أقل درجة بعد التصحيح"],
	["deadline", "الأقرب لانتهاء الوقت"]
], rt = new Intl.Collator("ar", {
	numeric: !0,
	sensitivity: "base"
}), it = new Intl.DateTimeFormat("ar-EG", {
	day: "numeric",
	month: "short",
	hour: "2-digit",
	minute: "2-digit",
	second: "2-digit",
	timeZone: "Africa/Cairo"
});
function at(e, t) {
	return t === "newest" || t === "oldest" ? e.joined_at || null : t === "seen" ? e.last_seen || null : t === "submitted" ? e.submitted_at || null : t === "highest" || t === "lowest" ? !e.cancelled_at && e.submitted_at && e.pending === 0 ? e.score : null : t === "deadline" && !e.cancelled_at && !e.submitted_at && e.deadline || null;
}
function ot(e, t) {
	return [...e].sort((e, n) => {
		let r = 0;
		if (t === "name" || t === "code") r = rt.compare(String(e[t] || ""), String(n[t] || ""));
		else {
			let i = at(e, t), a = at(n, t);
			if (i === null && a !== null) return 1;
			if (a === null && i !== null) return -1;
			i !== null && a !== null && (r = [
				"oldest",
				"lowest",
				"deadline"
			].includes(t) ? i - a : a - i);
		}
		return r || rt.compare(String(e.code), String(n.code)) || String(e.id).localeCompare(String(n.id));
	});
}
function st(e) {
	return e ? it.format(/* @__PURE__ */ new Date(e * 1e3)) : "لم يدخل بعد";
}
function ct(e, t) {
	let n = [], r = [];
	for (let i of e) (t(i) ? n : r).push(i);
	return [...n, ...r];
}
//#endregion
//#region src/RosterCards.jsx
function lt({ counts: e, filter: t, onChange: n, searching: r, showScreenCards: i }) {
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "roster-card-area",
		"aria-label": "حالات الطلاب",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "roster-card-heading",
				children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "حالات الطلاب" }), /* @__PURE__ */ (0, M.jsxs)("p", {
					className: "help",
					children: [r ? "الأعداد حسب البحث الحالي. " : "", "اختر الحالة لعرض طلابها."]
				})]
			}),
			[{
				label: "حالات الطلاب",
				filters: Ye
			}, ...i ? [{
				label: "مراقبة الشاشة",
				filters: Xe
			}] : []].map((r) => /* @__PURE__ */ (0, M.jsxs)(_.Fragment, { children: [r.label === "مراقبة الشاشة" && /* @__PURE__ */ (0, M.jsx)("h3", {
				className: "screen-card-heading",
				children: "آخر حالة للشاشة · المحاولات غير المسلّمة"
			}), /* @__PURE__ */ (0, M.jsx)("div", {
				className: "roster-status-cards",
				role: "group",
				"aria-label": r.label === "حالات الطلاب" ? "تصفية الطلاب" : "تصفية مراقبة الشاشة",
				children: r.filters.map(([r, i]) => /* @__PURE__ */ (0, M.jsxs)("button", {
					type: "button",
					"aria-label": `${i}: ${b(e[r])} طالب`,
					"aria-pressed": t === r,
					className: `roster-status-card ${t === r ? "selected" : ""}`,
					onClick: () => n(r),
					children: [/* @__PURE__ */ (0, M.jsx)("span", {
						className: "roster-card-label",
						children: i
					}), /* @__PURE__ */ (0, M.jsxs)("span", {
						className: "roster-card-bottom",
						children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: b(e[r]) }), /* @__PURE__ */ (0, M.jsx)("span", {
							"aria-hidden": "true",
							children: t === r ? "✓" : "←"
						})]
					})]
				}, r))
			})] }, r.label)),
			/* @__PURE__ */ (0, M.jsxs)("p", {
				className: "roster-card-selection",
				role: "status",
				children: [
					"المعروض: ",
					[...Ye, ...Xe].find(([e]) => e === t)?.[1],
					" · ",
					b(e[t]),
					" طالب"
				]
			})
		]
	});
}
//#endregion
//#region src/ScreenControls.jsx
function ut({ exam: e, request: t, refresh: n }) {
	let [r, i] = (0, _.useState)(!1), a = async (r) => {
		i(!0);
		try {
			await t(`/api/exams/${e.id}/screen-rule`, { enabled: r }), await n(), T("تم حفظ مراقبة الشاشة");
		} catch (e) {
			T(e.message, !0);
		} finally {
			i(!1);
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel padded",
		children: [
			/* @__PURE__ */ (0, M.jsx)("h2", { children: "مراقبة شاشة الطالب" }),
			/* @__PURE__ */ (0, M.jsxs)("label", {
				className: "checkbox-row",
				children: [/* @__PURE__ */ (0, M.jsx)("input", {
					type: "checkbox",
					checked: e.config.screenGuard === !0,
					disabled: r,
					onChange: (e) => a(e.target.checked)
				}), " إيقاف الحل عند الخروج من ملء الشاشة أو انخفاض مساحة العرض"]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الطالب يوافق بزر عند بدء الحل. انخفاض أكثر من ٢٥٪ أو الخروج من ملء الشاشة لمدة ٥ ثوانٍ يوقفه حتى تضغط «استكمال الطالب». الوقت يستمر والإجابات محفوظة."
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "علامة المتصفح ليست بصمة جهاز مضمونة. قياس الشاشة والخروج إشارات يرسلها المتصفح، وقد تتأثر بتقسيم الشاشة أو إعدادات الجهاز. الكيبورد والتدوير يُراعَيان قدر الإمكان؛ الأجهزة غير الداعمة لملء الشاشة تراقَب بالمساحة فقط."
			})
		]
	});
}
//#endregion
//#region src/AbsenceControls.jsx
function dt({ exam: e, request: t, refresh: n }) {
	let [r, i] = (0, _.useState)(e.config.absenceSeconds || 0), [a, o] = (0, _.useState)(!1);
	return (0, _.useEffect)(() => i(e.config.absenceSeconds || 0), [e.config.absenceSeconds]), /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel padded",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "الخروج من صفحة الامتحان" }), /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "حدد مدة الغياب بنفسك. صفر يعني أن الإيقاف يدوي فقط."
				})] }), /* @__PURE__ */ (0, M.jsx)("span", {
					className: "badge quiet",
					children: e.config.absenceSeconds ? `إيقاف بعد ${b(e.config.absenceSeconds)} ثانية` : "إيقاف يدوي"
				})]
			}),
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "actions",
				children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["مدة الغياب بالثواني", /* @__PURE__ */ (0, M.jsx)("input", {
					type: "number",
					min: "0",
					max: "3600",
					step: "1",
					value: r,
					onChange: (e) => i(Number(e.target.value))
				})] }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					disabled: a || !Number.isInteger(r) || r < 0 || r > 3600 || r > 0 && r < 5,
					onClick: async () => {
						o(!0);
						try {
							await t(`/api/exams/${e.id}/absence-rule`, { seconds: r }), await n(), T("تم حفظ إعداد الغياب");
						} catch (e) {
							T(e.message, !0);
						} finally {
							o(!1);
						}
					},
					children: "حفظ مدة الغياب"
				})]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الإيقاف يمنع الحل والتسليم، والتايمر يظل شغالًا. تغيير التطبيق أو انقطاع الاتصال قد يُحسب غيابًا. الاستكمال بقرارك من صف الطالب."
			})
		]
	});
}
//#endregion
//#region src/centerRoomSelection.js
function ft(e, t, n = !1) {
	if (n) return null;
	let r = e.find((e) => e.id === t), i = e.find((e) => ["waiting", "running"].includes(e.state));
	return r ? r.state === "closed" && i && i.createdAt > r.createdAt ? i.id : null : i?.id || e[0]?.id || null;
}
//#endregion
//#region src/centerEmbeddedTheme.js
var pt = {
	light: {
		canvas: "#F6F7F8",
		surface: "#FFFFFF",
		ink: "#0A1D3D",
		muted: "#4C596A",
		line: "#DCE1E6",
		accent: "#087B7B",
		subtle: "#EEF1F4",
		success: "#0B695F",
		successSurface: "#E8F4F2",
		warning: "#75500B",
		warningSurface: "#FFF5DD",
		error: "#9F2121",
		errorSurface: "#FFEEEE",
		nav: "#0A1D3D",
		textOnNav: "#E6EDF5",
		onPrimary: "#FFFFFF"
	},
	dark: {
		canvas: "#0C1220",
		surface: "#141E2F",
		ink: "#E6EDF5",
		muted: "#ADBCCD",
		line: "#344257",
		accent: "#64D9CC",
		subtle: "#1E2B40",
		success: "#77DFC4",
		successSurface: "#15352D",
		warning: "#FFD58A",
		warningSurface: "#3C2C17",
		error: "#FFADAA",
		errorSurface: "#3B222B",
		nav: "#0A1020",
		textOnNav: "#E6EDF5",
		onPrimary: "#092625"
	}
}, mt = {
	canvas: "canvas",
	surface: "paper",
	ink: "ink",
	muted: "muted",
	line: "line",
	accent: "teal",
	subtle: "subtle",
	success: "success",
	successSurface: "teal-soft",
	warning: "warning",
	warningSurface: "warning-soft",
	error: "danger",
	errorSurface: "danger-soft",
	nav: "center-nav",
	textOnNav: "center-text-on-nav",
	onPrimary: "on-primary"
};
function ht(e) {
	let t = e?.mode === "dark" ? "dark" : "light", n = { ...pt[t] };
	for (let t of Object.keys(n)) {
		let r = e?.colors?.[t] ?? (t === "onPrimary" ? e?.onPrimary : void 0);
		typeof r == "string" && /^#[\da-f]{6}$/i.test(r) && (n[t] = r);
	}
	return {
		mode: t,
		colors: n
	};
}
function gt(e, t) {
	if (new URLSearchParams(e.location.search).get("embedded") !== "center") return !1;
	t.documentElement.classList.add("center-embedded-root"), t.body.classList.add("center-embedded");
	let n = (e) => {
		let n = ht(e), r = t.documentElement;
		r.dataset.centerTheme = n.mode, r.style.colorScheme = n.mode;
		for (let [e, t] of Object.entries(mt)) r.style.setProperty(`--${t}`, n.colors[e]);
		return r.style.setProperty("--navy", n.colors.ink), !0;
	};
	return e.applyMassarCenterTheme = n, n(e.__massarCenterTheme || { mode: new URLSearchParams(e.location.search).get("theme") }), !0;
}
//#endregion
//#region src/centerNativeNavigation.js
var _t = /* @__PURE__ */ new Set([
	"room",
	"exams",
	"sheets",
	"catalog",
	"whatsapp",
	"storage",
	"guide"
]), vt = /* @__PURE__ */ new WeakMap();
function yt(e) {
	return e === "editor" ? "exams" : _t.has(e) ? e : null;
}
function bt(e, t, n) {
	e.chrome.webview.postMessage({
		kind: "massar-center-navigation",
		nonce: t.nonce,
		documentId: t.documentId,
		...n
	});
}
function xt(e, t, n) {
	let r = vt.get(e), i = yt(t);
	r && i && (r.view = i, bt(e, r, {
		action: "state",
		destination: i,
		...n ? { requestId: n } : {}
	}));
}
function St(e, t, { navigate: n, canLeave: r, onError: i }) {
	let a = new URLSearchParams(e.location.search), o = e.__massarCenterNavigationNonce;
	if (a.get("embedded") !== "center" || a.get("native-shell") !== "1" || e.location.protocol !== "http:" || e.location.hostname !== "127.0.0.1" || e.top !== e || typeof o != "string" || !/^[a-f0-9]{48}$/.test(o) || !e.chrome?.webview?.postMessage || !e.chrome?.webview?.addEventListener) return () => {};
	let s = vt.get(e);
	if (!s) {
		let t = e.crypto.getRandomValues(/* @__PURE__ */ new Uint8Array(16));
		s = {
			nonce: o,
			documentId: Array.from(t, (e) => e.toString(16).padStart(2, "0")).join(""),
			view: "room",
			tail: Promise.resolve()
		}, vt.set(e, s);
	}
	s.navigate = n, s.canLeave = r, s.onError = i, t.body.classList.add("center-native-shell");
	let c = (t) => {
		let n = t.data;
		n?.kind !== "massar-center-navigation" || n.nonce !== s.nonce || n.documentId !== s.documentId || !Number.isSafeInteger(n.requestId) || n.requestId < 1 || !["navigate", "leave"].includes(n.action) || n.action === "navigate" && !_t.has(n.destination) || (s.tail = s.tail.then(async () => {
			try {
				if (n.action === "leave") {
					let t = await s.canLeave();
					bt(e, s, {
						action: "leave-result",
						requestId: n.requestId,
						allowed: t === !0
					});
				} else xt(e, await s.navigate(n.destination) === !0 ? n.destination : s.view, n.requestId);
			} catch (t) {
				n.action === "leave" ? bt(e, s, {
					action: "leave-result",
					requestId: n.requestId,
					allowed: !1
				}) : xt(e, s.view, n.requestId), s.onError?.(t);
			}
		}));
	};
	return e.chrome.webview.addEventListener("message", c), bt(e, s, {
		action: "ready",
		destination: s.view
	}), () => e.chrome.webview.removeEventListener("message", c);
}
//#endregion
//#region src/admin.jsx
var Ct = gt(window, document), wt = {
	whatsapp: "واتساب السنتر",
	"whatsapp-center": "إرسال تقرير السنتر",
	sheets: "شيتات",
	room: "قاعة السنتر",
	catalog: "تنظيم السنتر",
	exams: "مكتبة الامتحانات",
	storage: "الإعدادات والبيانات",
	guide: "دليل التشغيل",
	editor: "تجهيز امتحان"
}, I = ["waiting", "running"], Tt = "احفظ الدرجة وأغلق مراجعة الطالب أولًا", Et = () => !!document.getElementById("review-panel");
function Dt() {
	if (Et()) throw Error(Tt);
	if (Array.from(document.querySelectorAll("dialog[open],[role=dialog]")).some((e) => e.getClientRects().length > 0)) throw Error("أكمل العملية المفتوحة أو أغلق النافذة أولًا");
}
var Ot = (e) => /* @__PURE__ */ (0, M.jsx)("span", {
	className: `badge ${e}`,
	children: C[e]
});
function kt({ url: e }) {
	let t = re.encodeText(e, re.Ecc.MEDIUM), n = [];
	for (let e = 0; e < t.size; e++) for (let r = 0; r < t.size; r++) t.getModule(r, e) && n.push(`M${r + 4},${e + 4}h1v1h-1z`);
	return /* @__PURE__ */ (0, M.jsxs)("figure", {
		className: "student-qr",
		children: [/* @__PURE__ */ (0, M.jsxs)("svg", {
			className: "qr",
			viewBox: `0 0 ${t.size + 8} ${t.size + 8}`,
			role: "img",
			"aria-label": "رمز دخول الطلاب",
			children: [/* @__PURE__ */ (0, M.jsx)("rect", {
				width: "100%",
				height: "100%",
				fill: "white"
			}), /* @__PURE__ */ (0, M.jsx)("path", {
				d: n.join(""),
				fill: "#0A1D3D"
			})]
		}), /* @__PURE__ */ (0, M.jsx)("figcaption", {
			dir: "ltr",
			children: e
		})]
	});
}
function At() {
	let [e, t] = (0, _.useState)(null), [n, r] = (0, _.useState)(!1), [i, a] = (0, _.useState)([]), [o, s] = (0, _.useState)([]), [c, l] = (0, _.useState)({
		grades: [],
		centers: [],
		groups: [],
		lessons: []
	}), [u, d] = (0, _.useState)(null), [f, p] = (0, _.useState)(null), [m, h] = (0, _.useState)(null), [g, v] = (0, _.useState)([
		"storage",
		"whatsapp",
		"whatsapp-center"
	].includes(new URLSearchParams(location.search).get("view")) ? new URLSearchParams(location.search).get("view") : "room"), y = (0, _.useRef)(g), b = (0, _.useCallback)((e) => {
		y.current = e, v(e);
	}, []), [x, S] = (0, _.useState)(null), [C, w] = (0, _.useState)(!1), D = (0, _.useRef)(!1), O = (0, _.useCallback)((e) => {
		D.current = e, w(e);
	}, []), [k, ee] = (0, _.useState)(!1), ne = (0, _.useRef)(!1), re = (0, _.useCallback)((e) => {
		ne.current = e, ee(e);
	}, []), [j, ie] = (0, _.useState)(!0), [ae, oe] = (0, _.useState)(""), se = (0, _.useRef)(null), le = (e, t) => E(e, t, se.current), ue = async () => {
		let e = await E("/api/exams");
		return a(e.exams), e.exams;
	}, de = async () => {
		let e = await E("/api/templates");
		return s(e.templates), e.templates;
	}, fe = async () => {
		let e = await E("/api/catalog");
		return l(e), e;
	}, pe = async (e) => {
		let t = await E(`/api/exams/${e}`);
		p(t), h(e), b("room"), O(!1);
	}, me = async (e) => {
		if (!ne.current) {
			re(!0);
			try {
				await e();
			} catch (e) {
				T(e.message || "تعذر الاتصال بالجهاز", !0);
			} finally {
				re(!1);
			}
		}
	}, he = async (e) => {
		if (Ct && Dt(), ne.current) throw Error("انتظر انتهاء العملية الحالية");
		re(!0);
		try {
			return y.current === "editor" && D.current && !await te("مغادرة التعديلات بدون حفظ؟", "التغييرات التي كتبتها لم تُحفظ.") ? !1 : (e === "sheets" && await Promise.all([ue(), fe()]), e === "exams" && await de(), e === "catalog" && await fe(), b(e), O(!1), !0);
		} finally {
			re(!1);
		}
	};
	(0, _.useEffect)(() => {
		let e = !0;
		return (async () => {
			try {
				let n = new URLSearchParams(location.hash.slice(1)).get("center-access");
				n && (history.replaceState(null, "", location.pathname + location.search), await E("/api/center/browser-login", { capability: n }));
				let i = await E("/api/bootstrap");
				if (!e) return;
				if (se.current = i.token, t(i), i.centerIntegrated && !Ct) {
					let e = document.createElement("p");
					e.className = "notice", e.textContent = "اختر الامتحان والمجموعة والحصة على الساكند من الدليل المحفوظ، وافتح القاعة دون اتصال بالهوست. بعد التصحيح أعد الاتصال وحمّل الدرجات.", document.querySelector(".sidebar")?.appendChild(e);
				}
				let [o, c, u] = await Promise.all([
					E("/api/exams"),
					E("/api/templates"),
					E("/api/catalog")
				]);
				if (!e) return;
				a(o.exams), s(c.templates), l(u);
				let d = o.exams;
				if (d.length) {
					let t = d.find((e) => I.includes(e.state)) || d[0], n = await E(`/api/exams/${t.id}`);
					e && (p(n), h(t.id));
				}
				e && r(!0);
			} catch (t) {
				e && oe(t.message);
			}
		})(), () => {
			e = !1;
		};
	}, []), (0, _.useEffect)(() => {
		document.body.classList.toggle("center-whatsapp-view", Ct && g === "whatsapp-center"), document.querySelectorAll(".nav-item").forEach((e) => {
			e.classList.toggle("active", e.dataset.view === g), e.dataset.view === g ? e.setAttribute("aria-current", "page") : e.removeAttribute("aria-current");
		});
		let e = document.getElementById("page-location");
		e && (e.textContent = wt[g]);
	}, [g]), (0, _.useEffect)(() => {
		let e = (e) => {
			let t = e.target.closest(".nav-item");
			t && he(t.dataset.view).catch((e) => T(e.message, !0));
		}, t = document.querySelector(".sidebar nav");
		return t?.addEventListener("click", e), () => t?.removeEventListener("click", e);
	}, [
		g,
		C,
		k
	]), (0, _.useEffect)(() => {
		let e = document.getElementById("backup");
		if (!e) return;
		let t = () => me(async () => {
			await A("/api/backup", `massar-backup-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.sqlite3`, "application/octet-stream") && T("تم تجهيز النسخة الاحتياطية");
		});
		return e.addEventListener("click", t), () => e.removeEventListener("click", t);
	}, [k]), (0, _.useEffect)(() => {
		if (!e) return;
		let n = !0, r = setInterval(async () => {
			try {
				let r = await E("/api/status");
				if (n && Array.isArray(r.studentUrls) && t((e) => !e || JSON.stringify(e.studentUrls) === JSON.stringify(r.studentUrls) ? e : {
					...e,
					studentUrls: r.studentUrls
				}), e.centerIntegrated && g === "room" && !k) {
					let e = await E("/api/exams");
					if (!n) return;
					a(e.exams);
					let t = !!(document.querySelector("#review-panel,dialog[open]") || document.activeElement?.matches("input,textarea,select")), r = ft(e.exams, m, t);
					if (r) {
						let e = await E(`/api/exams/${r}`);
						n && (p(e), h(r), ie(!0));
						return;
					}
				}
				if (g === "room" && m && !k) {
					let e = await E(`/api/exams/${m}`);
					n && (f && e.exam.state !== f.exam.state && await ue(), p(e));
				}
				n && ie(!0);
			} catch {
				n && ie(!1);
			}
		}, 3e3);
		return () => {
			n = !1, clearInterval(r);
		};
	}, [
		e,
		g,
		m,
		k,
		f?.exam.state
	]), (0, _.useEffect)(() => {
		let e = document.getElementById("admin-connection");
		e && (e.classList.toggle("offline", !j), e.textContent = j ? "● متصل بجهاز الإدارة" : "انقطع الاتصال بجهاز الإدارة");
	}, [j]), (0, _.useEffect)(() => {
		if (n) return St(window, document, {
			navigate: async (e) => {
				if (ne.current) throw Error("انتظر انتهاء العملية الحالية");
				return he(e);
			},
			canLeave: () => {
				if (Dt(), ne.current) throw Error("انتظر انتهاء العملية الحالية");
				return y.current !== "editor" || !D.current || te("مغادرة التعديلات بدون حفظ؟", "التغييرات التي كتبتها لم تُحفظ.");
			},
			onError: (e) => T(e.message, !0)
		});
	}, [
		n,
		g,
		C,
		k
	]), (0, _.useEffect)(() => {
		n && xt(window, g);
	}, [n, g]), (0, _.useEffect)(() => {
		let e = (e) => {
			y.current === "editor" && D.current && (e.preventDefault(), e.returnValue = "");
		};
		return window.addEventListener("beforeunload", e), () => window.removeEventListener("beforeunload", e);
	}, [g, C]);
	let ge = async (e) => {
		re(!0);
		try {
			await le(u ? `/api/templates/${u}/save` : "/api/templates", e), await de(), b("exams"), O(!1), T("تم حفظ الامتحان في المكتبة");
		} finally {
			re(!1);
		}
	}, _e = async (t, n, r, i) => {
		re(!0);
		try {
			let a = await le("/api/room/open", e.centerIntegrated ? r ? {
				standalone: !0,
				requestId: r,
				templateId: n,
				...i ? { cachedContextId: i } : {}
			} : {
				contextId: t,
				templateId: n
			} : {
				lessonId: t,
				templateId: n
			});
			await ue(), await pe(a.id), T("تم فتح قاعة السنتر للحصة المختارة");
		} catch (e) {
			T(e.message, !0);
		} finally {
			re(!1);
		}
	}, N = (e) => me(async () => {
		(e !== "start" || await te("بدء الامتحان الآن؟", "سيبدأ الوقت لكل الطلاب الموجودين. تأكد أن الطلاب مستعدون.")) && (e !== "close" || await te("إنهاء هذه القاعة؟", "ستُقفل محاولات طلاب هذه القاعة فقط، وتُعتمد آخر إجابات وصلت للجهاز.")) && (await le(`/api/exams/${m}/${e}`, {}), await ue(), await pe(m), T(e === "start" ? "بدأ الامتحان" : e === "publish" ? "قاعة الانتظار مفتوحة" : "انتهى الامتحان وحُفظت الإجابات"));
	}), P = (e) => {
		d(e?.id || null), S(e?.config || null), b("editor"), O(!1);
	};
	return ae ? /* @__PURE__ */ (0, M.jsxs)("div", {
		className: "empty",
		children: [
			/* @__PURE__ */ (0, M.jsx)("h1", { children: "تعذر فتح الإدارة" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: ae }),
			/* @__PURE__ */ (0, M.jsx)("a", {
				className: "button",
				href: "/",
				children: "إعادة المحاولة"
			})
		]
	}) : e ? g === "sheets" ? /* @__PURE__ */ (0, M.jsx)(qe, {
		catalog: c,
		exams: i
	}) : g === "storage" ? /* @__PURE__ */ (0, M.jsx)(Ce, {
		centerIntegrated: e.centerIntegrated,
		token: e.token,
		onWhatsApp: () => b("whatsapp")
	}) : g === "whatsapp-center" ? /* @__PURE__ */ (0, M.jsx)(Ue, { batchId: new URLSearchParams(location.search).get("batch") || "" }) : g === "whatsapp" ? /* @__PURE__ */ (0, M.jsx)(Le, {
		token: e.token,
		returnBatchId: new URLSearchParams(location.search).get("batch") || ""
	}) : g === "guide" ? /* @__PURE__ */ (0, M.jsx)(Ut, { centerIntegrated: e.centerIntegrated }) : g === "catalog" ? /* @__PURE__ */ (0, M.jsx)(Ge, {
		catalog: c,
		request: le,
		refresh: fe
	}) : g === "editor" ? /* @__PURE__ */ (0, M.jsx)(ce, {
		config: x,
		onDirty: O,
		onSave: ge,
		onCancel: () => he("exams").catch((e) => T(e.message, !0))
	}, u || "new") : g === "exams" ? /* @__PURE__ */ (0, M.jsx)(jt, {
		templates: o,
		onEdit: P,
		onNew: () => P(null)
	}) : Ct ? /* @__PURE__ */ (0, M.jsx)(Ft, {
		dashboard: f,
		bootstrap: e,
		busy: k,
		onState: N,
		request: le,
		refresh: () => pe(m),
		onLibrary: () => he("exams").catch((e) => T(e.message, !0)),
		launcher: /* @__PURE__ */ (0, M.jsx)(Mt, {
			centerIntegrated: e.centerIntegrated,
			catalog: c,
			templates: o,
			exams: i,
			onOpen: _e,
			onHistory: pe,
			busy: k,
			request: le,
			currentId: m
		})
	}) : f ? /* @__PURE__ */ (0, M.jsx)(Rt, {
		dashboard: f,
		bootstrap: e,
		busy: k,
		onState: N,
		request: le,
		refresh: () => pe(m),
		launcher: /* @__PURE__ */ (0, M.jsx)(Mt, {
			centerIntegrated: e.centerIntegrated,
			catalog: c,
			templates: o,
			exams: i,
			onOpen: _e,
			onHistory: pe,
			busy: k,
			request: le,
			currentId: m
		})
	}, m) : /* @__PURE__ */ (0, M.jsx)(Mt, {
		centerIntegrated: e.centerIntegrated,
		catalog: c,
		templates: o,
		exams: i,
		onOpen: _e,
		onHistory: pe,
		busy: k,
		request: le
	}) : /* @__PURE__ */ (0, M.jsx)("div", {
		className: "loading",
		children: "جارٍ فتح قاعة السنتر…"
	});
}
function jt({ templates: e, onEdit: t, onNew: n }) {
	let [r, i] = (0, _.useState)(""), a = e.filter((e) => String(e.config.title || "").toLowerCase().includes(r.toLowerCase()));
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsxs)("div", {
		className: "page-heading",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
			/* @__PURE__ */ (0, M.jsx)("span", {
				className: "context-label",
				children: "جهّز الامتحان مرة واستخدمه في أي حصة"
			}),
			/* @__PURE__ */ (0, M.jsx)("h1", { children: "مكتبة الامتحانات" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: "كل امتحان محفوظ باسمه وأسئلته؛ تختاره عند فتح قاعة السنتر." })
		] }), /* @__PURE__ */ (0, M.jsx)("button", {
			className: "button",
			onClick: n,
			children: "＋ إنشاء امتحان"
		})]
	}), /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel library-panel",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "library-toolbar",
			children: [/* @__PURE__ */ (0, M.jsxs)("label", {
				className: "search-field",
				children: [/* @__PURE__ */ (0, M.jsx)("span", {
					className: "sr-only",
					children: "بحث في الامتحانات"
				}), /* @__PURE__ */ (0, M.jsx)("input", {
					placeholder: "ابحث باسم الامتحان…",
					value: r,
					onChange: (e) => i(e.target.value)
				})]
			}), /* @__PURE__ */ (0, M.jsxs)("span", {
				className: "muted",
				children: [b(e.length), " امتحان محفوظ"]
			})]
		}), /* @__PURE__ */ (0, M.jsx)("div", {
			className: "exam-list",
			children: a.length ? a.map((e) => /* @__PURE__ */ (0, M.jsxs)("button", {
				className: "exam-list-row",
				onClick: () => t(e),
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: e.config.title }), /* @__PURE__ */ (0, M.jsxs)("small", { children: [
					b(e.config.questions?.length || 0),
					" سؤال · ",
					b(e.config.minutes),
					" دقيقة"
				] })] }), /* @__PURE__ */ (0, M.jsx)("span", {
					className: "exam-open-label",
					children: "فتح وتعديل ←"
				})]
			}, e.id)) : /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "empty",
				children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "لا يوجد امتحان بهذا الاسم" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "أنشئ امتحانًا جديدًا أو غيّر البحث." })]
			})
		})]
	})] });
}
function Mt({ catalog: e, templates: t, exams: n, onOpen: r, onHistory: i, busy: a, currentId: o, request: s, centerIntegrated: c = !1 }) {
	let [l, u] = (0, _.useState)(""), [d, f] = (0, _.useState)(""), [p, m] = (0, _.useState)(""), [h, g] = (0, _.useState)(""), [v, y] = (0, _.useState)(""), x = (t, n, r) => (e[t] || []).filter((e) => e[n] === r), S = n.filter((e) => I.includes(e.state)), [C, w] = (0, _.useState)(null), [D, O] = (0, _.useState)(!1);
	(0, _.useEffect)(() => {
		let e = !0;
		return E("/api/settings/rooms").then((t) => {
			e && w(t.multipleRooms);
		}).catch((e) => T(e.message, !0)), () => {
			e = !1;
		};
	}, []);
	let k = async (e) => {
		O(!0);
		try {
			let t = await s("/api/settings/rooms", { multipleRooms: e });
			w(t.multipleRooms), T(e ? "تم تفعيل تشغيل أكثر من قاعة" : "تم الرجوع لقاعة واحدة");
		} catch (e) {
			T(e.message, !0);
		} finally {
			O(!1);
		}
	}, ee = (t) => {
		let n = e.lessons?.find((e) => e.id === t);
		if (!n) return c ? "الرصد بعد النقل للهوست" : "جلسة قديمة";
		let r = e.groups?.find((e) => e.id === n.groupId);
		return [
			e.centers?.find((e) => e.id === r?.centerId)?.name,
			r?.name,
			n.name
		].filter(Boolean).join(" · ");
	}, A = /* @__PURE__ */ (0, M.jsxs)("label", {
		className: "multiple-rooms-option",
		children: [/* @__PURE__ */ (0, M.jsx)("input", {
			type: "checkbox",
			checked: C === !0,
			disabled: a || D || C === null,
			onChange: (e) => k(e.target.checked)
		}), /* @__PURE__ */ (0, M.jsxs)("span", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "تشغيل أكثر من قاعة في نفس الوقت" }), /* @__PURE__ */ (0, M.jsx)("small", { children: "قاعة واحدة: دخول مباشر. أكثر من قاعة: اختيار إجباري للطلاب الجدد. الطلاب الموجودون يظلون في قاعتهم." })] })]
	}), te = n.length > 0 && /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel history-panel",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "padded section-top",
			children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "سجل الجلسات والنتائج" }), /* @__PURE__ */ (0, M.jsxs)("span", {
				className: "muted",
				children: [b(n.length), " جلسة"]
			})]
		}), n.map((e) => /* @__PURE__ */ (0, M.jsxs)("button", {
			className: `exam-list-row ${e.id === o ? "selected" : ""}`,
			onClick: () => i(e.id),
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: e.title }), /* @__PURE__ */ (0, M.jsxs)("small", { children: [
					ee(e.lessonId),
					" · ",
					(/* @__PURE__ */ new Date(e.createdAt * 1e3)).toLocaleDateString("ar-EG"),
					" · ",
					b(e.submitted),
					" تسليم"
				] })] }),
				Ot(e.state),
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "exam-open-label",
					children: "عرض ←"
				})
			]
		}, e.id))]
	});
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsx)("div", {
			className: "page-heading",
			children: /* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: "قاعة السنتر"
				}),
				/* @__PURE__ */ (0, M.jsx)("h1", { children: c ? "اختر الامتحان والمجموعة والحصة" : "اختر الحصة والامتحان" }),
				/* @__PURE__ */ (0, M.jsx)("p", { children: c ? "اختَر الامتحان والمجموعة والحصة من دليل السنتر المحفوظ تلقائيًا. لا تحتاج اتصالًا بالهوست أو كشف طلاب لفتح القاعة." : "الامتحانات تُجهّز في المكتبة. هنا تربط نسخة من الامتحان بالحصة وتفتحها للطلاب." })
			] })
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel launcher-panel",
			children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "section-top",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "فتح جلسة جديدة" }), /* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: C ? "يمكن فتح أكثر من قاعة؛ الطلاب الجدد يختارون القاعة عند وجود قاعتين أو أكثر." : "يمكن فتح جلسة واحدة في الوقت نفسه."
					})] }), S.length > 0 && /* @__PURE__ */ (0, M.jsxs)("span", {
						className: "badge waiting",
						children: [b(S.length), " قاعة مفتوحة"]
					})]
				}),
				Ct ? /* @__PURE__ */ (0, M.jsxs)("details", {
					className: "center-room-options",
					children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "إعدادات القاعات" }), A]
				}) : A,
				!c && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "launcher-grid",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["الصف الدراسي", /* @__PURE__ */ (0, M.jsxs)("select", {
							value: l,
							onChange: (e) => {
								u(e.target.value), f(""), m(""), g("");
							},
							children: [/* @__PURE__ */ (0, M.jsx)("option", {
								value: "",
								children: "اختر الصف"
							}), e.grades?.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e.id,
								children: e.name
							}, e.id))]
						})] }),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["السنتر", /* @__PURE__ */ (0, M.jsxs)("select", {
							value: d,
							disabled: !l,
							onChange: (e) => {
								f(e.target.value), m(""), g("");
							},
							children: [/* @__PURE__ */ (0, M.jsx)("option", {
								value: "",
								children: "اختر السنتر"
							}), x("centers", "gradeId", l).map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e.id,
								children: e.name
							}, e.id))]
						})] }),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["المجموعة", /* @__PURE__ */ (0, M.jsxs)("select", {
							value: p,
							disabled: !d,
							onChange: (e) => {
								m(e.target.value), g("");
							},
							children: [/* @__PURE__ */ (0, M.jsx)("option", {
								value: "",
								children: "اختر المجموعة"
							}), x("groups", "centerId", d).map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e.id,
								children: e.name
							}, e.id))]
						})] }),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["الحصة", /* @__PURE__ */ (0, M.jsxs)("select", {
							value: h,
							disabled: !p,
							onChange: (e) => g(e.target.value),
							children: [/* @__PURE__ */ (0, M.jsx)("option", {
								value: "",
								children: "اختر الحصة"
							}), x("lessons", "groupId", p).map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e.id,
								children: e.name
							}, e.id))]
						})] }),
						/* @__PURE__ */ (0, M.jsxs)("label", { children: ["اسم الامتحان", /* @__PURE__ */ (0, M.jsxs)("select", {
							value: v,
							onChange: (e) => y(e.target.value),
							children: [/* @__PURE__ */ (0, M.jsx)("option", {
								value: "",
								children: "اختر الامتحان"
							}), t.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e.id,
								children: e.config.title
							}, e.id))]
						})] })
					]
				}), /* @__PURE__ */ (0, M.jsxs)("div", {
					className: "actions launcher-actions",
					children: [
						/* @__PURE__ */ (0, M.jsx)("button", {
							className: "button",
							disabled: a || D || C === null || !h || !v || !C && S.length > 0,
							onClick: () => r(h, v),
							children: "فتح قاعة السنتر ←"
						}),
						(!e.grades?.length || !e.lessons?.length) && /* @__PURE__ */ (0, M.jsx)("span", {
							className: "help",
							children: "أضف الصف والسنتر والمجموعة والحصة من «تنظيم السنتر» أولًا."
						}),
						!t.length && /* @__PURE__ */ (0, M.jsx)("span", {
							className: "help",
							children: "أنشئ امتحانًا من «مكتبة الامتحانات» أولًا."
						})
					]
				})] }),
				c && /* @__PURE__ */ (0, M.jsx)(Nt, {
					templates: t,
					onOpen: r,
					disabled: a || D || C === null || !C && S.length > 0
				})
			]
		}),
		n.length > 0 && (Ct ? /* @__PURE__ */ (0, M.jsxs)("details", {
			className: "center-room-history",
			children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "سجل الجلسات والنتائج" }), te]
		}) : te)
	] });
}
function Nt({ templates: e, onOpen: t, disabled: n }) {
	let [r, i] = (0, _.useState)(""), [a, o] = (0, _.useState)(""), [s, c] = (0, _.useState)(""), [l, u] = (0, _.useState)(""), [d, f] = (0, _.useState)({
		contexts: [],
		updatedAt: null
	}), [p, m] = (0, _.useState)(!0), [h, g] = (0, _.useState)(""), v = (0, _.useRef)(null);
	(0, _.useEffect)(() => {
		let e = !0, t = () => E("/api/room/cached-catalog").then((t) => {
			e && (f(t), g(""));
		}).catch((t) => {
			e && g(t.message);
		}).finally(() => {
			e && m(!1);
		});
		t();
		let n = setInterval(t, 5e3);
		return () => {
			e = !1, clearInterval(n);
		};
	}, []);
	let y = d.contexts || [], x = (e, t) => [...new Map(e.map((e) => [e[t], e])).values()], S = x(y, "groupId"), C = x(y.filter((e) => e.groupId === a), "sessionId"), w = y.filter((e) => e.groupId === a && e.sessionId === s), T = w.find((e) => e.activityId === l), D = (e) => {
		v.current = null, e();
	}, O = (e) => {
		let n = e?.id || "";
		(!v.current || v.current.contextId !== n) && (v.current = {
			id: crypto.randomUUID(),
			contextId: n
		}), t(null, r, v.current.id, n);
	};
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		p && /* @__PURE__ */ (0, M.jsx)("p", {
			role: "status",
			children: "جارٍ قراءة دليل المجموعات والحصص المحفوظ…"
		}),
		!p && d.updatedAt && /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "help",
			children: [
				"دليل السنتر محفوظ على هذا الجهاز · آخر تحديث ",
				(/* @__PURE__ */ new Date(d.updatedAt * 1e3)).toLocaleString("ar-EG"),
				". يعمل دون اتصال بالهوست."
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "launcher-grid",
			children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["اسم الامتحان", /* @__PURE__ */ (0, M.jsxs)("select", {
				value: r,
				onChange: (e) => D(() => i(e.target.value)),
				children: [/* @__PURE__ */ (0, M.jsx)("option", {
					value: "",
					children: "اختر الامتحان من المكتبة"
				}), e.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
					value: e.id,
					children: e.config.title
				}, e.id))]
			})] }), y.length > 0 && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["المجموعة", /* @__PURE__ */ (0, M.jsxs)("select", {
					value: a,
					disabled: p,
					onChange: (e) => D(() => {
						o(e.target.value), c(""), u("");
					}),
					children: [/* @__PURE__ */ (0, M.jsx)("option", {
						value: "",
						children: "اختر المجموعة"
					}), S.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
						value: e.groupId,
						children: e.groupLabel
					}, e.groupId))]
				})] }),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["الحصة الفعلية", /* @__PURE__ */ (0, M.jsxs)("select", {
					value: s,
					disabled: !a || p,
					onChange: (e) => D(() => {
						c(e.target.value), u("");
					}),
					children: [/* @__PURE__ */ (0, M.jsx)("option", {
						value: "",
						children: "اختر الحصة"
					}), C.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
						value: e.sessionId,
						children: e.sessionLabel
					}, e.sessionId))]
				})] }),
				/* @__PURE__ */ (0, M.jsxs)("label", { children: ["امتحان الرصد", /* @__PURE__ */ (0, M.jsxs)("select", {
					value: l,
					disabled: !s || p,
					onChange: (e) => D(() => u(e.target.value)),
					children: [/* @__PURE__ */ (0, M.jsx)("option", {
						value: "",
						children: "اختر امتحان الرصد"
					}), w.map((e) => /* @__PURE__ */ (0, M.jsxs)("option", {
						value: e.activityId,
						children: [
							e.activityName,
							" · من ",
							b(e.maxScore)
						]
					}, e.activityId))]
				})] })
			] })]
		}),
		T && /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "help",
			children: [
				"مجموع امتحان الرصد: ",
				b(T.maxScore),
				". اختر نموذجًا يطابق المجموع المسموح: ",
				(T.allowedMaxScores || [T.maxScore]).map(b).join(" / "),
				"."
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "actions launcher-actions",
			children: [/* @__PURE__ */ (0, M.jsx)("button", {
				className: "button",
				disabled: n || p || !r || y.length > 0 && !T,
				onClick: () => O(T),
				children: "فتح قاعة السنتر ←"
			}), y.length > 0 && /* @__PURE__ */ (0, M.jsx)("button", {
				className: "button secondary",
				disabled: n || p || !r,
				onClick: () => O(null),
				children: "فتح دون ربط حصة"
			})]
		}),
		!p && !y.length && /* @__PURE__ */ (0, M.jsx)("p", {
			className: "notice",
			children: "دليل الحصص غير موجود بعد. يمكنك فتح الامتحان الآن، ثم اختيار وجهة الرصد على الهوست بعد الاتصال. يُحفظ الدليل تلقائيًا عند ربط الساكند بالهوست وتسجيل دخول الموظف."
		}),
		h && /* @__PURE__ */ (0, M.jsxs)("p", {
			role: "alert",
			className: "notice",
			children: [
				"تعذر تحديث دليل الحصص: ",
				h,
				". الامتحانات المحفوظة ما زالت متاحة."
			]
		}),
		/* @__PURE__ */ (0, M.jsx)("p", {
			className: "help",
			children: "الطالب يكتب اسمه ورقم الموبايل والكود. لا يحتاج حضورًا سابقًا؛ الإجابات والدرجات محفوظة على الساكند، وتنتقل للهوست بعد التصحيح."
		}),
		!e.length && /* @__PURE__ */ (0, M.jsx)("p", {
			className: "help",
			children: "أضف نموذجًا من مكتبة الامتحانات أولًا."
		}),
		/* @__PURE__ */ (0, M.jsxs)("details", { children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "قاعة مرتبطة بكشف مجهّز سابقًا — اختياري" }), /* @__PURE__ */ (0, M.jsx)(Pt, {
			templates: e,
			onOpen: t,
			disabled: n
		})] })
	] });
}
function Pt({ templates: e, onOpen: t, disabled: n }) {
	let [r, i] = (0, _.useState)([]), [a, o] = (0, _.useState)(""), [s, c] = (0, _.useState)(""), [l, u] = (0, _.useState)("");
	(0, _.useEffect)(() => {
		let e = !0;
		return E("/api/room/center-choices").then((t) => {
			e && i(t.contexts);
		}).catch((t) => {
			e && u(t.message);
		}), () => {
			e = !1;
		};
	}, []);
	let d = r.find((e) => e.id === a);
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "launcher-grid",
			children: [/* @__PURE__ */ (0, M.jsxs)("label", { children: ["الحصة المجهّزة من السنتر", /* @__PURE__ */ (0, M.jsxs)("select", {
				value: a,
				onChange: (e) => o(e.target.value),
				children: [/* @__PURE__ */ (0, M.jsx)("option", {
					value: "",
					children: "اختر المجموعة والحصة والامتحان في الرصد"
				}), r.map((e) => /* @__PURE__ */ (0, M.jsxs)("option", {
					value: e.id,
					children: [
						e.groupLabel,
						" · ",
						e.sessionLabel,
						" · ",
						e.activityName
					]
				}, e.id))]
			})] }), /* @__PURE__ */ (0, M.jsxs)("label", { children: ["اسم الامتحان", /* @__PURE__ */ (0, M.jsxs)("select", {
				value: s,
				onChange: (e) => c(e.target.value),
				children: [/* @__PURE__ */ (0, M.jsx)("option", {
					value: "",
					children: "اختر الامتحان من المكتبة"
				}), e.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
					value: e.id,
					children: e.config.title
				}, e.id))]
			})] })]
		}),
		d && /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "help",
			children: [
				"الدرجة النهائية في الرصد: ",
				b(d.maxScore),
				". اختر امتحانًا بنفس المجموع."
			]
		}),
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "actions launcher-actions",
			children: [
				/* @__PURE__ */ (0, M.jsx)("button", {
					className: "button",
					disabled: n || !a || !s,
					onClick: () => t(a, s),
					children: "فتح قاعة السنتر ←"
				}),
				!r.length && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "وصّل الكابل، ثم جهّز كشف الحصة من شاشة «تجهيز الحصة وكشف الطلاب» في البرنامج قبل الدخول للقاعة."
				}),
				!e.length && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "help",
					children: "أضف نموذجًا من مكتبة الامتحانات أولًا."
				})
			]
		}),
		l && /* @__PURE__ */ (0, M.jsx)("p", {
			role: "alert",
			className: "notice",
			children: l
		})
	] });
}
function Ft({ dashboard: e, bootstrap: t, busy: n, onState: r, request: i, refresh: a, onLibrary: o, launcher: s }) {
	let c = e?.exam, l = () => c ? c.state === "closed" ? "grades" : "monitor" : "setup", [u, d] = (0, _.useState)(l), f = (e) => {
		if (e !== u) try {
			Dt(), d(e);
		} catch (e) {
			T(e.message, !0);
		}
	}, p = (0, _.useRef)(c?.id);
	(0, _.useEffect)(() => {
		p.current !== c?.id && (p.current = c?.id, d(l()));
	}, [c?.id]), (0, _.useEffect)(() => {
		c?.state === "closed" && d("grades");
	}, [c?.state]);
	let m = e?.attempts.filter((e) => e.joined_at && !e.cancelled_at) || [], h = m.filter((e) => e.submitted_at), g = h.filter((e) => e.pending > 0), v = e?.centerTarget, y = [
		v?.groupLabel,
		v?.sessionLabel,
		v?.activityName
	].filter(Boolean).join(" · ") || (v ? "الحصة مرتبطة بالسنتر؛ تفاصيل وجهة الرصد تظهر على الهوست." : "قاعة دون ربط حصة. تُحدد وجهة الرصد على الهوست عند نقل النتائج.");
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "center-exam-flow",
		"aria-label": "إدارة الامتحانات",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("header", {
				className: "page-heading",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("h1", { children: "إدارة الامتحانات" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "افتح القاعة، تابع الطلاب، ثم صحّح وانقل النتائج للهوست." })] }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary",
					onClick: o,
					children: "مكتبة الامتحانات"
				})]
			}),
			/* @__PURE__ */ (0, M.jsx)("nav", {
				className: "center-exam-stages",
				"aria-label": "مراحل إدارة الامتحان",
				children: [
					["setup", "فتح القاعة"],
					["monitor", "متابعة الطلاب"],
					["grades", "التصحيح والرصد"]
				].map(([e, t]) => /* @__PURE__ */ (0, M.jsx)("button", {
					type: "button",
					"aria-pressed": u === e,
					disabled: e !== "setup" && !c,
					onClick: () => f(e),
					children: t
				}, e))
			}),
			u === "setup" ? /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "center-exam-setup",
				children: [c && /* @__PURE__ */ (0, M.jsxs)("div", {
					className: "center-room-context",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: c.config.title }), /* @__PURE__ */ (0, M.jsx)("p", { children: y })] }),
						Ot(c.state),
						/* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary small",
							onClick: () => f(c.state === "closed" ? "grades" : "monitor"),
							children: "العودة للجلسة الحالية"
						})
					]
				}), s]
			}) : /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "center-room-context",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: c.config.title }), /* @__PURE__ */ (0, M.jsx)("p", { children: y })] }), /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary small",
						onClick: () => f("setup"),
						children: "الجلسات وبيانات القاعة"
					})]
				}),
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "center-room-toolbar",
					children: [/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "center-room-summary",
						"aria-label": "ملخص القاعة",
						children: [
							Ot(c.state),
							/* @__PURE__ */ (0, M.jsxs)("span", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: b(m.length) }), " دخلوا القاعة"] }),
							/* @__PURE__ */ (0, M.jsxs)("span", { children: [
								/* @__PURE__ */ (0, M.jsx)("strong", { children: b(u === "grades" ? h.length - g.length : h.length) }),
								" ",
								u === "grades" ? "جاهزة للرصد" : "سلّموا"
							] }),
							/* @__PURE__ */ (0, M.jsxs)("span", { children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: b(g.length) }), " ينتظرون التصحيح"] })
						]
					}), /* @__PURE__ */ (0, M.jsxs)("div", {
						className: "actions",
						children: [
							c.state === "draft" && /* @__PURE__ */ (0, M.jsx)("button", {
								className: "button",
								disabled: n,
								onClick: () => r("publish"),
								children: "فتح قاعة الانتظار ←"
							}),
							c.state === "waiting" && /* @__PURE__ */ (0, M.jsx)("button", {
								className: "button",
								disabled: n,
								onClick: () => r("start"),
								children: "ابدأ الامتحان للجميع ←"
							}),
							I.includes(c.state) && /* @__PURE__ */ (0, M.jsx)("button", {
								className: "button secondary danger-text",
								disabled: n,
								onClick: () => r("close"),
								children: "إنهاء الجلسة"
							})
						]
					})]
				}),
				c.state === "running" && /* @__PURE__ */ (0, M.jsx)(Je, {
					dashboard: e,
					request: i,
					refresh: a
				}),
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "center-room-disclosures",
					children: [/* @__PURE__ */ (0, M.jsxs)("details", {
						className: "center-room-link",
						children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "رابط دخول الطلاب ورمز QR" }), /* @__PURE__ */ (0, M.jsx)(Lt, {
							exam: c,
							bootstrap: t
						})]
					}), /* @__PURE__ */ (0, M.jsxs)("details", {
						className: "center-room-settings",
						children: [
							/* @__PURE__ */ (0, M.jsx)("summary", { children: "الأسئلة وإعدادات المراقبة" }),
							/* @__PURE__ */ (0, M.jsxs)("details", { children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "عرض أسئلة الجلسة" }), /* @__PURE__ */ (0, M.jsx)(zt, { exam: c })] }),
							I.includes(c.state) && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)(dt, {
								exam: c,
								request: i,
								refresh: a
							}), /* @__PURE__ */ (0, M.jsx)(ut, {
								exam: c,
								request: i,
								refresh: a
							})] })
						]
					})]
				}),
				u === "grades" && /* @__PURE__ */ (0, M.jsx)("p", {
					className: "center-recording-note",
					children: "تُحفظ النتائج على الساكند. بعد اكتمال التصحيح، أعد توصيل الجهازين واختر «تحميل الدرجات» على الهوست لتأكيد الرصد دفعة واحدة. تحتفظ كل نتيجة بمجموعها الفعلي."
				}),
				/* @__PURE__ */ (0, M.jsx)("section", {
					className: "center-room-roster",
					"aria-label": u === "grades" ? "التصحيح والرصد" : "متابعة الطلاب",
					children: /* @__PURE__ */ (0, M.jsx)(Bt, {
						dashboard: e,
						tab: u === "grades" ? "results" : "students",
						request: i,
						refresh: a,
						busy: n
					}, `${c.id}-${u}`)
				})
			] })
		]
	});
}
function It({ counts: e, filter: t, onChange: n, filters: r, tab: i }) {
	let a = i === "results" ? [
		"all",
		"pending",
		"graded",
		"problem"
	] : [
		"all",
		"solving",
		"submitted",
		"problem"
	], o = ([r, i]) => /* @__PURE__ */ (0, M.jsxs)("button", {
		type: "button",
		className: `filter-chip ${t === r ? "active" : ""}`,
		"aria-label": `${i}: ${b(e[r])} طالب`,
		"aria-pressed": t === r,
		onClick: () => n(r),
		children: [i, /* @__PURE__ */ (0, M.jsx)("strong", { children: b(e[r]) })]
	}, r);
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "center-roster-filters",
		"aria-label": "حالات الطلاب",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", {
			role: "group",
			"aria-label": "تصفية الطلاب",
			children: [r.filter(([e]) => a.includes(e)).map(o), /* @__PURE__ */ (0, M.jsxs)("details", {
				className: "center-extra-filters",
				children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "فلاتر أخرى" }), /* @__PURE__ */ (0, M.jsx)("div", {
					role: "group",
					"aria-label": "حالات ومراقبة إضافية",
					children: r.filter(([e]) => !a.includes(e)).map(o)
				})]
			})]
		}), /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "help",
			role: "status",
			children: [
				"المعروض: ",
				r.find(([e]) => e === t)?.[1],
				" · ",
				b(e[t]),
				" طالب"
			]
		})]
	});
}
function Lt({ exam: e, bootstrap: t }) {
	let n = me(pe()), [r, i] = (0, _.useState)(""), a = n || (t.studentUrls.includes(r) ? r : t.studentUrls[0]);
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "join-panel",
		children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
			/* @__PURE__ */ (0, M.jsx)("span", {
				className: "context-label",
				children: "دخول الطلاب"
			}),
			/* @__PURE__ */ (0, M.jsx)("h2", { children: "رابط دخول الطلاب" }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: t.centerIntegrated ? "الطالب يتصل بالواي فاي، يفتح الرابط، ثم يكتب اسمه ورقم تلفونه وكوده ويدخل مباشرةً." : "الطالب يتصل بالواي فاي، يمسح الرمز، ثم يدخل اسمه ورقمه وكوده." }),
			t.centerIntegrated && /* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "الإجابات والدرجات محفوظة على الساكند. بعد التصحيح أعد ربط الجهازين، واختر وجهة الرصد وحمّل الدرجات من الهوست."
			}),
			!n && t.studentUrls.length > 1 && /* @__PURE__ */ (0, M.jsxs)("label", { children: ["اختر عنوان الشبكة المتصل بها الطلاب", /* @__PURE__ */ (0, M.jsx)("select", {
				value: a,
				onChange: (e) => i(e.target.value),
				children: t.studentUrls.map((e) => /* @__PURE__ */ (0, M.jsx)("option", {
					value: e,
					children: e
				}, e))
			})] }),
			a ? /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "link-field",
				children: [/* @__PURE__ */ (0, M.jsx)("input", {
					readOnly: !0,
					dir: "ltr",
					value: a
				}), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button secondary small",
					onClick: () => O(a).catch((e) => T(e.message, !0)),
					children: "نسخ الرابط"
				})]
			}) : /* @__PURE__ */ (0, M.jsx)("p", {
				className: "inline-error",
				children: "لا يوجد عنوان شبكة محلية للكمبيوتر. اتصل بشبكة الهوت سبوت أو الراوتر أولًا."
			}),
			n && /* @__PURE__ */ (0, M.jsxs)("details", {
				className: "notice",
				children: [
					/* @__PURE__ */ (0, M.jsx)("summary", { children: "رابط محلي بديل قبل تسجيل الدخول فقط" }),
					/* @__PURE__ */ (0, M.jsxs)("p", { children: [
						"الرابط الأساسي والرمز يفتحان نفس عنوان صفحة الدخول التلقائي. إذا تعذّر فتحه قبل دخول الامتحان، استخدم ",
						/* @__PURE__ */ (0, M.jsx)("b", {
							dir: "ltr",
							children: "http://10.77.0.1/"
						}),
						"."
					] }),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary small",
						onClick: () => O("http://10.77.0.1/").catch((e) => T(e.message, !0)),
						children: "نسخ الرابط المحلي البديل"
					}),
					/* @__PURE__ */ (0, M.jsx)("p", { children: "من بدأ الامتحان يستمر في نفس الرابط والمتصفح؛ الانتقال إلى عنوان آخر لا ينقل الجلسة والإجابات المحفوظة على الموبايل." })
				]
			}),
			t.centerIntegrated ? /* @__PURE__ */ (0, M.jsx)("button", {
				className: "text-button",
				onClick: () => O(t.localStudentUrl).catch((e) => T(e.message, !0)),
				children: "نسخ رابط تجربة الطالب على هذا الجهاز"
			}) : /* @__PURE__ */ (0, M.jsx)("a", {
				className: "text-link",
				href: t.localStudentUrl,
				target: "_blank",
				rel: "noopener",
				children: "فتح صفحة طالب على هذا الجهاز ↗"
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "شارك الرابط بعد اتصال الطلاب بنفس الشبكة. يختار الطالب «البقاء متصلًا بالشبكة» ويوقف التحويل التلقائي لبيانات الهاتف. الرابط والرمز للطلاب الجدد؛ من بدأ الامتحان يستمر في نفس العنوان والمتصفح حتى التسليم."
			})
		] }), a && /* @__PURE__ */ (0, M.jsx)(kt, { url: a })]
	});
}
function Rt({ dashboard: e, bootstrap: t, busy: n, onState: r, request: i, refresh: a, launcher: o }) {
	let s = e.exam, [c, l] = (0, _.useState)(s.state === "closed" ? "results" : "students");
	(0, _.useEffect)(() => {
		s.state === "closed" && l("results");
	}, [s.state]);
	let u = e.attempts.filter((e) => e.joined_at), d = u.filter((t) => e.serverTime - t.last_seen < 15).length, f = u.filter((e) => !e.cancelled_at && e.submitted_at).length, p = u.filter((e) => !e.cancelled_at && e.submitted_at && e.pending > 0).length;
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
		o,
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "page-heading room-current-heading",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
				/* @__PURE__ */ (0, M.jsx)("span", {
					className: "context-label",
					children: {
						draft: "جهّز امتحانك، ثم افتح القاعة.",
						waiting: "القاعة جاهزة لاستقبال الطلاب.",
						running: "الامتحان جارٍ. تابع القاعة.",
						closed: "انتهى الامتحان. راجع النتائج."
					}[s.state]
				}),
				/* @__PURE__ */ (0, M.jsx)("h1", { children: s.config.title }),
				/* @__PURE__ */ (0, M.jsxs)("div", {
					className: "exam-meta",
					children: [
						Ot(s.state),
						/* @__PURE__ */ (0, M.jsxs)("span", { children: [b(s.config.questionCount), " أسئلة"] }),
						/* @__PURE__ */ (0, M.jsxs)("span", { children: [b(s.config.minutes), " دقيقة"] }),
						/* @__PURE__ */ (0, M.jsx)("span", { children: s.config.timerMode === "shared" ? "نهاية موحدة" : "وقت مستقل لكل طالب" })
					]
				})
			] }), /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "actions",
				children: [
					s.state === "draft" && /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						disabled: n,
						onClick: () => r("publish"),
						children: "فتح قاعة الانتظار ←"
					}),
					s.state === "waiting" && /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button",
						disabled: n,
						onClick: () => r("start"),
						children: "ابدأ الامتحان للجميع ←"
					}),
					I.includes(s.state) && /* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary danger-text",
						disabled: n,
						onClick: () => r("close"),
						children: "إنهاء الجلسة"
					})
				]
			})]
		}),
		s.state === "running" && /* @__PURE__ */ (0, M.jsx)(Je, {
			dashboard: e,
			request: i,
			refresh: a
		}),
		I.includes(s.state) && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)(dt, {
			exam: s,
			request: i,
			refresh: a
		}), /* @__PURE__ */ (0, M.jsx)(ut, {
			exam: s,
			request: i,
			refresh: a
		})] }),
		/* @__PURE__ */ (0, M.jsx)("ol", {
			className: "exam-steps",
			"aria-label": "مراحل الامتحان",
			children: [
				["draft", "تجهيز الأسئلة"],
				["waiting", "استقبال الطلاب"],
				["running", "حل الامتحان"],
				["closed", "مراجعة النتائج"]
			].map(([e, t], n) => /* @__PURE__ */ (0, M.jsxs)("li", {
				className: s.state === e ? "current" : "",
				children: [/* @__PURE__ */ (0, M.jsx)("span", { children: b(n + 1) }), t]
			}, e))
		}),
		/* @__PURE__ */ (0, M.jsxs)("div", {
			className: `room-overview room-state-${s.state}`,
			children: [/* @__PURE__ */ (0, M.jsx)(Lt, {
				exam: s,
				bootstrap: t
			}), /* @__PURE__ */ (0, M.jsxs)("section", {
				className: "attendance-summary",
				"aria-label": "ملخص الحضور",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("span", { children: "دخل القاعة" }), /* @__PURE__ */ (0, M.jsx)("strong", { children: b(u.length) })] }),
					/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("span", { children: "متصل الآن" }), /* @__PURE__ */ (0, M.jsx)("strong", { children: b(d) })] }),
					/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("span", { children: "سلّم الامتحان" }), /* @__PURE__ */ (0, M.jsx)("strong", { children: b(f) })] }),
					/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsx)("span", { children: "ينتظر تصحيح المقالي" }), /* @__PURE__ */ (0, M.jsx)("strong", { children: b(p) })] })
				]
			})]
		}),
		/* @__PURE__ */ (0, M.jsxs)("section", {
			className: "panel roster-panel",
			children: [/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "panel-toolbar",
				children: [/* @__PURE__ */ (0, M.jsx)("div", {
					className: "tabs",
					role: "group",
					"aria-label": "عرض القاعة",
					children: [
						["students", "الطلاب والحضور"],
						["questions", "الأسئلة"],
						["results", "التصحيح والنتائج"]
					].map(([e, t]) => /* @__PURE__ */ (0, M.jsx)("button", {
						className: c === e ? "active" : "",
						onClick: () => l(e),
						children: t
					}, e))
				}), /* @__PURE__ */ (0, M.jsxs)("span", {
					className: "muted",
					children: [b(e.attempts.length), " طالب في الجلسة"]
				})]
			}), c === "questions" ? /* @__PURE__ */ (0, M.jsx)(zt, { exam: s }) : /* @__PURE__ */ (0, M.jsx)(Bt, {
				dashboard: e,
				tab: c,
				request: i,
				refresh: a,
				busy: n
			}, c)]
		})
	] });
}
function zt({ exam: e }) {
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("div", {
		className: "section-top padded",
		children: /* @__PURE__ */ (0, M.jsx)("p", {
			className: "muted",
			children: "الأسئلة نسخة محفوظة لهذه الجلسة. أي تعديل في المكتبة يؤثر في الجلسات الجديدة فقط."
		})
	}), /* @__PURE__ */ (0, M.jsx)("div", {
		className: "question-previews",
		children: e.config.questions.map((e, t) => /* @__PURE__ */ (0, M.jsxs)("article", { children: [/* @__PURE__ */ (0, M.jsxs)("div", {
			className: "section-top",
			children: [/* @__PURE__ */ (0, M.jsxs)("h3", { children: [
				b(t + 1),
				". ",
				e.text
			] }), /* @__PURE__ */ (0, M.jsxs)("span", {
				className: "badge quiet",
				children: [b(e.points), " درجات"]
			})]
		}), /* @__PURE__ */ (0, M.jsxs)("p", {
			className: "model-answer-preview",
			children: ["الإجابة النموذجية: ", e.kind === "mcq" ? e.options[e.correct] : e.modelAnswer]
		})] }, e.id))
	})] });
}
function Bt({ dashboard: e, tab: t, request: n, refresh: r, busy: i }) {
	let [a, o] = (0, _.useState)(""), [s, c] = (0, _.useState)("newest"), [l, u] = (0, _.useState)(!0), [d, f] = (0, _.useState)("all"), [p, m] = (0, _.useState)(null), [h, g] = (0, _.useState)(null), [v, y] = (0, _.useState)([]), x = e.exam, C = a.toLowerCase().trim(), w = e.attempts.filter((e) => `${e.name || ""} ${e.code} ${e.phone || ""}`.toLowerCase().includes(C)), E = (t) => Ze(t, x, e.serverTime), D = x.config.screenGuard === !0 || e.attempts.some((e) => e.screen), k = D ? [...Ye, ...Xe] : Ye, ee = Ct ? [...k, ["problem", "يحتاج متابعة"]] : k, ne = (t, n) => n === "problem" ? !!$e(t, x, e.serverTime) : Qe(t, E(t), n), re = Object.fromEntries(ee.map(([e]) => [e, w.filter((t) => ne(t, e)).length])), j = ee.some(([e]) => e === d) ? d : "all", ie = ot(w.filter((e) => ne(e, j)), s), ae = (t) => $e(t, x, e.serverTime), oe = ie.filter(ae).length, se = l ? ct(ie, ae) : ie, ce = (t) => x.state === "running" && t.joined_at && !t.cancelled_at && !t.submitted_at && t.deadline > e.serverTime, le = e.attempts.filter((e) => v.includes(e.id) && ce(e)), ue = se.filter(ce), de = (e, t) => y((n) => t ? [.../* @__PURE__ */ new Set([...n, e])] : n.filter((t) => t !== e)), fe = async (e) => {
		if (await te("استعادة دخول الطالب؟", "سيتم تسجيل خروج الجلسة القديمة. يدخل الطالب بنفس الكود والرقم، وتبقى الإجابات والوقت كما هما.")) try {
			await n(`/api/attempts/${e.id}/reset-login`, {}), T("يمكن للطالب الدخول مجددًا بنفس الكود والرقم"), await r();
		} catch (e) {
			T(e.message, !0);
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("div", {
		id: "room-content",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "roster-tools",
				children: [
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "search-field",
						children: [/* @__PURE__ */ (0, M.jsx)("span", {
							className: "sr-only",
							children: "بحث بالاسم أو الكود"
						}), /* @__PURE__ */ (0, M.jsx)("input", {
							placeholder: "ابحث باسم الطالب أو كوده…",
							value: a,
							onChange: (e) => o(e.target.value)
						})]
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "roster-order",
						children: ["ترتيب الطلاب", /* @__PURE__ */ (0, M.jsx)("select", {
							value: s,
							onChange: (e) => c(e.target.value),
							children: nt.map(([e, t]) => /* @__PURE__ */ (0, M.jsx)("option", {
								value: e,
								children: t
							}, e))
						})]
					}),
					/* @__PURE__ */ (0, M.jsxs)("label", {
						className: "roster-priority",
						title: "الموقوف، خارج الصفحة، غير المتصل، أو عنده تنبيه شاشة حالي",
						children: [
							/* @__PURE__ */ (0, M.jsx)("input", {
								type: "checkbox",
								checked: l,
								onChange: (e) => u(e.target.checked)
							}),
							"المشاكل أولًا ",
							/* @__PURE__ */ (0, M.jsx)("span", {
								className: "badge quiet",
								children: b(oe)
							})
						]
					}),
					/* @__PURE__ */ (0, M.jsx)("button", {
						className: "button secondary small",
						onClick: () => A(`/api/exams/${x.id}/${t === "students" ? "students.csv" : "results.csv"}`, t === "students" ? "students.csv" : "results.csv", "text/csv").catch((e) => T(e.message, !0)),
						children: t === "students" ? "تنزيل كشف الطلاب ↓" : "تنزيل النتائج CSV ↓"
					})
				]
			}),
			t === "results" && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("p", {
				className: "results-note",
				children: "الاختيارات تُصحح تلقائيًا. راجع المقالي قبل طباعة التقارير. الدرجات لا تظهر للطلاب."
			}), Ct ? /* @__PURE__ */ (0, M.jsxs)("details", {
				className: "center-batch-grading",
				children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "التصحيح الجماعي للمقالي وإعداداته" }), /* @__PURE__ */ (0, M.jsx)(Vt, {
					exam: x,
					dashboard: e,
					request: n,
					refresh: r
				})]
			}) : /* @__PURE__ */ (0, M.jsx)(Vt, {
				exam: x,
				dashboard: e,
				request: n,
				refresh: r
			})] }),
			t === "results" && (Ct ? /* @__PURE__ */ (0, M.jsxs)("details", {
				className: "center-result-exports",
				children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "تنزيل التقارير وإرسال واتساب" }), /* @__PURE__ */ (0, M.jsxs)("div", {
					className: "whatsapp-send-row",
					children: [/* @__PURE__ */ (0, M.jsx)(Ke, {
						examId: x.id,
						attempts: e.attempts
					}), /* @__PURE__ */ (0, M.jsx)(Re, {
						attempts: e.attempts.filter((e) => e.joined_at),
						label: "إرسال تقارير كل الطلاب عبر واتساب"
					})]
				})]
			}) : /* @__PURE__ */ (0, M.jsxs)("div", {
				className: "whatsapp-send-row",
				children: [/* @__PURE__ */ (0, M.jsx)(Ke, {
					examId: x.id,
					attempts: e.attempts
				}), /* @__PURE__ */ (0, M.jsx)(Re, {
					attempts: e.attempts.filter((e) => e.joined_at),
					label: "إرسال تقارير كل الطلاب عبر واتساب"
				})]
			})),
			Ct ? /* @__PURE__ */ (0, M.jsx)(It, {
				counts: re,
				filter: j,
				onChange: f,
				filters: ee,
				tab: t
			}) : /* @__PURE__ */ (0, M.jsx)(lt, {
				counts: re,
				filter: j,
				onChange: f,
				searching: !!C,
				showScreenCards: D
			}),
			x.state === "running" && /* @__PURE__ */ (0, M.jsx)(et, {
				examId: x.id,
				attempts: le,
				request: n,
				refresh: r,
				clear: () => y([])
			}),
			/* @__PURE__ */ (0, M.jsx)("div", {
				className: "table-scroll",
				children: /* @__PURE__ */ (0, M.jsxs)("table", { children: [/* @__PURE__ */ (0, M.jsx)("thead", { children: /* @__PURE__ */ (0, M.jsxs)("tr", { children: [
					x.state === "running" && /* @__PURE__ */ (0, M.jsx)("th", { children: /* @__PURE__ */ (0, M.jsx)("input", {
						type: "checkbox",
						"aria-label": "تحديد الطلاب الظاهرين المتاح تمديدهم",
						disabled: !ue.length,
						checked: ue.length > 0 && ue.every((e) => v.includes(e.id)),
						onChange: (e) => y((t) => e.target.checked ? [.../* @__PURE__ */ new Set([...t, ...ue.map((e) => e.id)])] : t.filter((e) => !ue.some((t) => t.id === e)))
					}) }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الطالب" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "كود الطالب" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الحالة" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: t === "results" ? "الدرجة" : "آخر ظهور" }),
					/* @__PURE__ */ (0, M.jsx)("th", { children: "الإجراء" })
				] }) }), /* @__PURE__ */ (0, M.jsx)("tbody", { children: se.length ? se.map((a) => {
					let o = E(a), s = ae(a), c = a.last_seen ? e.serverTime - a.last_seen < 15 ? "الآن" : `منذ ${b(Math.floor((e.serverTime - a.last_seen) / 60))} دقيقة` : "—", l = /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
						!a.cancelled_at && t === "results" && a.submitted_at ? /* @__PURE__ */ (0, M.jsx)("button", {
							className: "text-button",
							onClick: () => m(a.id),
							children: "مراجعة وتصحيح ←"
						}) : !a.cancelled_at && a.joined_at && !a.submitted_at ? /* @__PURE__ */ (0, M.jsx)("button", {
							className: "text-button",
							disabled: i,
							onClick: () => fe(a),
							children: "استعادة الدخول"
						}) : /* @__PURE__ */ (0, M.jsx)("span", {
							className: "muted",
							children: "—"
						}),
						!a.cancelled_at && x.state === "running" && a.joined_at && !a.submitted_at && /* @__PURE__ */ (0, M.jsx)("button", {
							className: "text-button",
							disabled: i,
							onClick: async () => {
								try {
									await n(`/api/attempts/${a.id}/${a.paused ? "resume" : "pause"}`, {}), await r(), T(a.paused ? "تم استكمال الطالب" : "تم إيقاف الطالب");
								} catch (e) {
									T(e.message, !0);
								}
							},
							children: a.paused ? "استكمال الطالب" : "إيقاف الطالب"
						}),
						!a.cancelled_at && a.joined_at && /* @__PURE__ */ (0, M.jsx)("button", {
							className: "text-button cancellation-action",
							disabled: i,
							onClick: () => g(a),
							children: "إلغاء المحاولة"
						})
					] });
					return /* @__PURE__ */ (0, M.jsxs)("tr", {
						className: s ? "roster-problem-row" : "",
						children: [
							x.state === "running" && /* @__PURE__ */ (0, M.jsx)("td", { children: /* @__PURE__ */ (0, M.jsx)("input", {
								type: "checkbox",
								"aria-label": `تحديد ${a.name} لتمديد الوقت`,
								disabled: !ce(a),
								checked: ce(a) && v.includes(a.id),
								onChange: (e) => de(a.id, e.target.checked)
							}) }),
							/* @__PURE__ */ (0, M.jsxs)("td", { children: [
								/* @__PURE__ */ (0, M.jsx)("strong", { children: a.name || "طالب" }),
								/* @__PURE__ */ (0, M.jsx)("small", {
									dir: "ltr",
									children: a.phone || "لم تُسجل بياناته"
								}),
								/* @__PURE__ */ (0, M.jsxs)("small", { children: ["الدخول: ", st(a.joined_at)] })
							] }),
							/* @__PURE__ */ (0, M.jsx)("td", { children: /* @__PURE__ */ (0, M.jsx)("button", {
								className: "code-button",
								title: "نسخ الكود",
								onClick: () => O(a.code).catch((e) => T(e.message, !0)),
								children: a.code
							}) }),
							/* @__PURE__ */ (0, M.jsxs)("td", { children: [
								/* @__PURE__ */ (0, M.jsx)("span", {
									className: `badge ${o.tone}`,
									children: o.label
								}),
								s && /* @__PURE__ */ (0, M.jsxs)("small", {
									className: "roster-problem-note",
									children: ["يحتاج متابعة: ", s]
								}),
								ce(a) && /* @__PURE__ */ (0, M.jsxs)("small", { children: ["الوقت المتبقي: ", /* @__PURE__ */ (0, M.jsx)("b", {
									dir: "ltr",
									children: S(Math.ceil(Math.max(0, a.deadline - e.serverTime)))
								})] }),
								a.cancelled_at && /* @__PURE__ */ (0, M.jsxs)("small", {
									className: "cancellation-reason",
									children: ["سبب الإلغاء: ", a.cancelReason]
								}),
								a.screen && /* @__PURE__ */ (0, M.jsxs)("small", {
									className: "screen-details",
									children: [
										"علامة المتصفح: ",
										/* @__PURE__ */ (0, M.jsx)("b", {
											dir: "ltr",
											children: a.screen.current.deviceId.slice(0, 8)
										}),
										/* @__PURE__ */ (0, M.jsx)("br", {}),
										"الشاشة: ",
										/* @__PURE__ */ (0, M.jsxs)("b", {
											dir: "ltr",
											children: [
												a.screen.current.screenWidth,
												" × ",
												a.screen.current.screenHeight
											]
										}),
										" · العرض: ",
										/* @__PURE__ */ (0, M.jsxs)("b", {
											dir: "ltr",
											children: [
												a.screen.current.width,
												" × ",
												a.screen.current.height
											]
										}),
										/* @__PURE__ */ (0, M.jsx)("br", {}),
										a.screen.current.fullscreenSupported ? a.screen.current.fullscreen ? "ملء الشاشة نشط" : "خارج ملء الشاشة" : "ملء الشاشة غير مدعوم",
										a.screen.lastEvent && /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
											/* @__PURE__ */ (0, M.jsx)("br", {}),
											"آخر تنبيه: ",
											a.screen.lastEvent,
											" · ",
											b(a.screen.events),
											" مرة"
										] })
									]
								}),
								x.config.screenGuard && !a.screen && !a.submitted_at && !a.cancelled_at && /* @__PURE__ */ (0, M.jsx)("small", { children: "في انتظار موافقة مراقبة الشاشة" }),
								a.departures > 0 && /* @__PURE__ */ (0, M.jsxs)("small", { children: [
									"غادر الصفحة ",
									b(a.departures),
									" مرة"
								] })
							] }),
							/* @__PURE__ */ (0, M.jsx)("td", { children: t === "results" ? a.cancelled_at ? "ملغي" : a.submitted_at ? /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
								b(a.score),
								" / ",
								b(a.maximum),
								a.pending > 0 && /* @__PURE__ */ (0, M.jsxs)("small", { children: [
									"متبقي ",
									b(a.pending),
									" مقالي"
								] })
							] }) : "لم يسلّم" : c }),
							/* @__PURE__ */ (0, M.jsx)("td", { children: Ct && t === "students" ? /* @__PURE__ */ (0, M.jsxs)("details", {
								className: "center-student-actions",
								children: [/* @__PURE__ */ (0, M.jsx)("summary", { children: "إجراءات الطالب" }), /* @__PURE__ */ (0, M.jsx)("div", { children: l })]
							}) : l })
						]
					}, a.id);
				}) : /* @__PURE__ */ (0, M.jsx)("tr", { children: /* @__PURE__ */ (0, M.jsx)("td", {
					colSpan: x.state === "running" ? 6 : 5,
					className: "empty",
					children: a || d !== "all" ? "لا يوجد طلاب مطابقون للبحث أو التصفية." : "الطالب يظهر هنا بعد دخوله باسمه ورقمه وكوده."
				}) }) })] })
			}),
			h && /* @__PURE__ */ (0, M.jsx)(tt, {
				attempt: h,
				request: n,
				refresh: r,
				onClose: () => g(null)
			}),
			p && /* @__PURE__ */ (0, M.jsx)(Ht, {
				attempt: e.attempts.find((e) => e.id === p),
				exam: x,
				request: n,
				onClose: () => m(null),
				refresh: r
			})
		]
	});
}
function Vt({ exam: e, dashboard: t, request: n, refresh: r }) {
	let [i, a] = (0, _.useState)(!1), [o, s] = (0, _.useState)(null), [c, l] = (0, _.useState)(!1), [u, d] = (0, _.useState)(""), [f, p] = (0, _.useState)(!1), m = t.attempts.reduce((e, t) => e + (t.cancelled_at ? 0 : t.pending || 0), 0);
	(0, _.useEffect)(() => {
		let t = !0;
		return n(`/api/exams/${e.id}/essay-batch`).then((e) => {
			t && s(e);
		}).catch((e) => {
			t && T(e.message, !0);
		}), () => {
			t = !1;
		};
	}, [e.id, t.serverTime]);
	let h = async (t) => {
		p(!0);
		try {
			await n(`/api/exams/${e.id}/essay-batch`, {
				...t,
				partialCredit: i
			}), l(!1), d(""), T("بدأ تصحيح المقالي. يمكن متابعة التقدم هنا."), s(await n(`/api/exams/${e.id}/essay-batch`));
		} catch (e) {
			T(e.message, !0);
		} finally {
			p(!1);
		}
	}, g = o?.job, v = g?.state === "running", y = g ? `${{
		running: "التصحيح جارٍ",
		completed: "اكتمل التصحيح",
		partial: "اكتمل مع إجابات بدون درجة",
		interrupted: "توقف التصحيح"
	}[g.state] || "حالة التصحيح"} · ${b(g.processed)} من ${b(g.total)} سؤال · ${b(g.graded)} صُحح · ${b(g.skipped)} كان مصححًا · ${b(g.failed)} بدون نتيجة مؤكدة.${g.needs_review ? ` ${b(g.needs_review)} إجابة معلقة من التصحيح السابق.` : ""} ${g.message || ""}` : `${b(m)} إجابة مقالية تنتظر التصحيح.`;
	return /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "essay-batch-panel",
		"aria-label": "التصحيح الجماعي للمقالي",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "section-top",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [
					/* @__PURE__ */ (0, M.jsx)("span", {
						className: "context-label",
						children: "تصحيح جماعي"
					}),
					/* @__PURE__ */ (0, M.jsx)("h2", { children: "المقالي لكل الطلاب" }),
					/* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "يصحح الإجابات المسلّمة غير المصححة فقط. يمكنك اختيار درجات جزئية وتعديل كل درجة يدويًا."
					})
				] }), /* @__PURE__ */ (0, M.jsx)("button", {
					className: "button",
					disabled: e.state !== "closed" || v || m === 0 || f || !o,
					onClick: () => o?.keyConfigured ? h({}) : l(!0),
					children: "تصحيح المقالي للجميع ✦"
				})]
			}),
			/* @__PURE__ */ (0, M.jsxs)("label", {
				className: "check-label",
				children: [/* @__PURE__ */ (0, M.jsx)("input", {
					type: "checkbox",
					checked: i,
					disabled: v || f,
					onChange: (e) => a(e.target.checked)
				}), "منح درجات جزئية للأجزاء الصحيحة في المقالي"]
			}),
			/* @__PURE__ */ (0, M.jsxs)("p", {
				className: "help",
				children: [i ? "توزيع درجة السؤال على الأجزاء المطلوبة، مع قبول الإجابات بنفس المعنى." : "التقدير الحالي: صح أو غلط فقط.", " ينطبق على الإجابات التي لم تُصحح بعد."]
			}),
			/* @__PURE__ */ (0, M.jsx)("p", {
				className: "help",
				children: "٥ طلبات معًا، ثم انتظار ٥ ثوانٍ بين الدفعات. أي طلب مؤقت لا يوقف بقية الدفعة ويعاد تلقائيًا. كل إجابة تحصل على درجة نهائية حسب المعنى والأجزاء الصحيحة المفهومة. يمكنك تعديل الدرجة يدويًا بعد التصحيح."
			}),
			/* @__PURE__ */ (0, M.jsxs)("p", {
				className: "help",
				role: "status",
				children: [
					o ? y : "جارٍ قراءة حالة التصحيح…",
					" ",
					b(m),
					" إجابة ما زالت بدون درجة.",
					v && /* @__PURE__ */ (0, M.jsx)("button", {
						className: "text-button",
						onClick: async () => {
							try {
								await n(`/api/exams/${e.id}/essay-batch/stop`, {}), T("سيقف التصحيح بعد السؤال الحالي");
							} catch (e) {
								T(e.message, !0);
							}
						},
						children: "إيقاف بعد السؤال الحالي"
					})
				]
			}),
			c && /* @__PURE__ */ (0, M.jsxs)("form", {
				onSubmit: (e) => {
					e.preventDefault(), h({ apiKey: u.trim() });
				},
				children: [
					/* @__PURE__ */ (0, M.jsxs)("label", { children: ["مفتاح خدمة التصحيح", /* @__PURE__ */ (0, M.jsx)("input", {
						type: "password",
						autoComplete: "off",
						value: u,
						onChange: (e) => d(e.target.value),
						placeholder: "الصق المفتاح هنا"
					})] }),
					/* @__PURE__ */ (0, M.jsx)("p", {
						className: "help",
						children: "يحتاج إنترنت على جهاز الإدارة. يُرسل نص السؤال والإجابة النموذجية وإجابة الطالب فقط. المفتاح يُحفظ في .env خارج SQLite."
					}),
					/* @__PURE__ */ (0, M.jsxs)("div", {
						className: "actions",
						children: [
							/* @__PURE__ */ (0, M.jsx)("button", {
								className: "button",
								disabled: f,
								type: "submit",
								children: "ابدأ التصحيح"
							}),
							/* @__PURE__ */ (0, M.jsx)("button", {
								className: "button secondary",
								type: "button",
								onClick: () => h({}),
								children: "استخدم المفتاح المحفوظ"
							}),
							/* @__PURE__ */ (0, M.jsx)("button", {
								className: "button secondary",
								type: "button",
								onClick: () => l(!1),
								children: "إلغاء"
							})
						]
					})
				]
			})
		]
	});
}
function Ht({ attempt: e, exam: t, request: n, onClose: r, refresh: i }) {
	let [a, o] = (0, _.useState)(!1);
	if (!e) return null;
	let s = e.questionIds.map((e) => t.config.questions.find((t) => t.id === e)).filter(Boolean), c = async (t, r) => {
		t.preventDefault();
		let a = t.currentTarget;
		o(!0);
		try {
			await n(`/api/attempts/${e.id}/grade`, {
				questionId: r.id,
				score: Number(a.elements.score.value),
				feedback: a.elements.feedback.value
			}), await i(), T("تم حفظ التصحيح");
		} catch (e) {
			T(e.message, !0);
		} finally {
			o(!1);
		}
	};
	return /* @__PURE__ */ (0, M.jsxs)("div", {
		id: "review-panel",
		children: [
			/* @__PURE__ */ (0, M.jsxs)("div", {
				className: "review-heading",
				children: [/* @__PURE__ */ (0, M.jsxs)("div", { children: [/* @__PURE__ */ (0, M.jsxs)("h2", { children: ["ورقة ", e.name] }), /* @__PURE__ */ (0, M.jsxs)("p", { children: [
					"الدرجة المصححة: ",
					b(e.score),
					" من ",
					b(e.maximum)
				] })] }), /* @__PURE__ */ (0, M.jsxs)("div", {
					className: "actions",
					children: [
						e.pending ? /* @__PURE__ */ (0, M.jsx)("span", {
							className: "badge waiting",
							children: "أكمل المقالي لفتح التقرير"
						}) : Ct ? /* @__PURE__ */ (0, M.jsx)("button", {
							className: "button secondary small",
							disabled: a,
							onClick: async () => {
								o(!0);
								try {
									await A(`/api/attempts/${e.id}/report.pdf`, "تقرير-الطالب.pdf", "application/pdf");
								} catch (e) {
									T(e.message, !0);
								} finally {
									o(!1);
								}
							},
							children: "تنزيل تقرير الطالب PDF"
						}) : /* @__PURE__ */ (0, M.jsx)("a", {
							className: "button secondary small",
							href: `/report/${e.id}`,
							children: "فتح تقرير الطالب / تنزيل PDF ↗"
						}),
						/* @__PURE__ */ (0, M.jsx)(Re, { attempts: [e] }),
						/* @__PURE__ */ (0, M.jsx)("button", {
							className: "text-button",
							disabled: a,
							onClick: r,
							children: "إغلاق"
						})
					]
				})]
			}),
			/* @__PURE__ */ (0, M.jsx)(ge, {
				attempt: e,
				exam: t,
				request: n,
				refresh: i
			}),
			s.map((t, n) => {
				let r = e.answers[t.id], i = e.grades[t.id];
				return /* @__PURE__ */ (0, M.jsxs)("article", {
					className: "review-question",
					children: [
						/* @__PURE__ */ (0, M.jsxs)("h3", { children: [
							b(n + 1),
							". ",
							t.text
						] }),
						/* @__PURE__ */ (0, M.jsxs)("p", {
							className: "answer-text",
							children: [
								/* @__PURE__ */ (0, M.jsx)("strong", { children: "إجابة الطالب:" }),
								" ",
								t.kind === "mcq" ? r == null ? "لم يجب" : t.options[r] : r || "لم يجب"
							]
						}),
						/* @__PURE__ */ (0, M.jsxs)("p", {
							className: "model-answer-preview",
							children: [
								/* @__PURE__ */ (0, M.jsx)("strong", { children: "الإجابة النموذجية:" }),
								" ",
								t.kind === "mcq" ? t.options[t.correct] : t.modelAnswer
							]
						}),
						t.kind === "essay" ? /* @__PURE__ */ (0, M.jsxs)("form", {
							className: "grade-form",
							onSubmit: (e) => c(e, t),
							children: [
								/* @__PURE__ */ (0, M.jsxs)("label", { children: [
									"الدرجة من ",
									b(t.points),
									/* @__PURE__ */ (0, M.jsx)("input", {
										name: "score",
										type: "number",
										required: !0,
										min: "0",
										max: t.points,
										step: "0.5",
										defaultValue: i?.score ?? ""
									})
								] }),
								/* @__PURE__ */ (0, M.jsxs)("label", { children: ["ملاحظة التصحيح", /* @__PURE__ */ (0, M.jsx)("input", {
									name: "feedback",
									maxLength: "2000",
									defaultValue: (i?.feedback || "").replace(/^Gemini:\s*/i, "")
								})] }),
								/* @__PURE__ */ (0, M.jsx)("button", {
									className: "button small",
									type: "submit",
									disabled: a,
									children: "حفظ الدرجة"
								})
							]
						}, `${t.id}:${i?.score}:${i?.feedback}`) : /* @__PURE__ */ (0, M.jsxs)("span", {
							className: "badge complete",
							children: [
								b(i?.score || 0),
								" / ",
								b(t.points)
							]
						})
					]
				}, t.id);
			})
		]
	});
}
function Ut({ centerIntegrated: e = !1 }) {
	return /* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [/* @__PURE__ */ (0, M.jsx)("div", {
		className: "page-heading",
		children: /* @__PURE__ */ (0, M.jsxs)("div", { children: [
			/* @__PURE__ */ (0, M.jsx)("span", {
				className: "context-label",
				children: "قبل أول تجربة"
			}),
			/* @__PURE__ */ (0, M.jsx)("h1", { children: "جهّز القاعة بخطوات بسيطة." }),
			/* @__PURE__ */ (0, M.jsx)("p", { children: "ابدأ بعدد قليل من الموبايلات، ثم زوّد العدد تدريجيًا." })
		] })
	}), /* @__PURE__ */ (0, M.jsxs)("section", {
		className: "panel guide",
		children: [/* @__PURE__ */ (0, M.jsxs)("ol", { children: [
			/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "وصّل الكمبيوتر والطلاب بنفس الشبكة" }), /* @__PURE__ */ (0, M.jsx)("p", { children: e ? "من برنامج السنتر افتح «التهيئة والبورتال» على جهاز الساكند، وافحص الاتصال قبل فتح القاعة. الطلاب يتصلون بواي فاي الامتحان ويفتحون الرابط الظاهر في القاعة. إن ظهر تنبيه «بلا إنترنت»، يختارون البقاء متصلين بالشبكة. يظل تسجيل الطلاب والحضور والتحصيل متاحًا على الهوست والساكند أثناء الاتصال." : "مع الـOmada المباشر، افتح «الإعدادات والبيانات» ثم «تهيئة الماك الآن». بعد اكتمالها، وصّل كابل الـOmada بالماك واترك محوّل كهربائه موصولًا. الطلاب يتصلون بواي فاي الامتحان؛ قد تظهر لهم صفحة الدخول تلقائيًا. لو لم تظهر، يفتحون رابط الطلاب المحلي. يمكن إبقاء الماك متصلًا بواي فاي آخر للإنترنت؛ شبكة الامتحان نفسها لا تحتاج إنترنت. لو ظهر للموبايل تنبيه «بلا إنترنت»، اختر البقاء متصلًا بهذه الشبكة." })] }),
			/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "جهّز الامتحان وافتح القاعة" }), /* @__PURE__ */ (0, M.jsx)("p", { children: e ? "على الساكند اختر الامتحان والمجموعة والحصة وامتحان الرصد من دليل السنتر المحفوظ تلقائيًا، وافتح القاعة دون اتصال بالهوست أو كشف طلاب. لو الدليل غير موجود، يمكنك فتح قاعة دون ربط واختيار الوجهة لاحقًا على الهوست. الطالب يكتب اسمه وتلفونه وكوده؛ ثم اضغط ابدأ الامتحان للجميع." : "الطالب يكتب اسمه ورقمه وكوده الحالي، ثم ينتظر بدء الامتحان." })] }),
			/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "ابدأ وتابع الحفظ" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "راقب المتصلين وحالة حفظ الإجابات. عند تغيير الموبايل استخدم «استعادة الدخول»." })] }),
			/* @__PURE__ */ (0, M.jsxs)("li", { children: [/* @__PURE__ */ (0, M.jsx)("h2", { children: "راجع واحتفظ بنسخة" }), /* @__PURE__ */ (0, M.jsx)("p", { children: e ? "أغلق القاعة وأكمل تصحيح المقالي، ثم أعد ربط الجهازين. على الهوست راجع وجهة الرصد المختارة، أو حددها لو فُتحت القاعة دون ربط، واضغط تأكيد استيراد الدرجات مرة واحدة. تُرصد الدرجات النهائية فقط، ومنها الصفر، ويظهر عدد المرصودين والباقين وأسباب الانتظار." : "بعد انتهاء الامتحان راجع المقالي واطبع تقرير كل طالب أو احفظه PDF، ثم نزّل نسخة احتياطية." })] })
		] }), /* @__PURE__ */ (0, M.jsxs)("div", {
			className: "notice",
			children: [/* @__PURE__ */ (0, M.jsx)("strong", { children: "حدود نسخة التجربة" }), /* @__PURE__ */ (0, M.jsx)("p", { children: "تصحيح المقالي الذكي يحتاج إنترنت على جهاز الإدارة. إرسال تقارير واتساب يحتاج حسابًا رسميًا وقالبًا معتمدًا وإنترنت على جهاز الإدارة. أبقِ الكمبيوتر مفتوحًا طوال الامتحان." })]
		})]
	})] });
}
(0, v.createRoot)(document.getElementById("main")).render(/* @__PURE__ */ (0, M.jsxs)(M.Fragment, { children: [
	/* @__PURE__ */ (0, M.jsx)(he, {}),
	/* @__PURE__ */ (0, M.jsx)(Be, {}),
	/* @__PURE__ */ (0, M.jsx)(At, {})
] }));
//#endregion
