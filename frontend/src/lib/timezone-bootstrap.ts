if (typeof Intl !== 'undefined' && Intl.DateTimeFormat) {
  const OriginalDateTimeFormat = Intl.DateTimeFormat;
  
  // Wrap constructor
  Intl.DateTimeFormat = function (locales?: any, options?: any) {
    const opts = { ...options };
    if (!opts.timeZone) {
      opts.timeZone = 'Africa/Cairo';
    }
    return new OriginalDateTimeFormat(locales, opts);
  } as any;
  
  // Restore prototype and statics
  Object.setPrototypeOf(Intl.DateTimeFormat, OriginalDateTimeFormat);
  
  // Hook Date.prototype methods to use default timeZone if not defined
  const originalToLocaleString = Date.prototype.toLocaleString;
  Date.prototype.toLocaleString = function (this: Date, locales?: any, options?: any) {
    const opts = { ...options } as any;
    if (!opts.timeZone) {
      opts.timeZone = 'Africa/Cairo';
    }
    return originalToLocaleString.call(this, locales, opts);
  } as any;

  const originalToLocaleDateString = Date.prototype.toLocaleDateString;
  Date.prototype.toLocaleDateString = function (this: Date, locales?: any, options?: any) {
    const opts = { ...options } as any;
    if (!opts.timeZone) {
      opts.timeZone = 'Africa/Cairo';
    }
    return originalToLocaleDateString.call(this, locales, opts);
  } as any;

  const originalToLocaleTimeString = Date.prototype.toLocaleTimeString;
  Date.prototype.toLocaleTimeString = function (this: Date, locales?: any, options?: any) {
    const opts = { ...options } as any;
    if (!opts.timeZone) {
      opts.timeZone = 'Africa/Cairo';
    }
    return originalToLocaleTimeString.call(this, locales, opts);
  } as any;
}
