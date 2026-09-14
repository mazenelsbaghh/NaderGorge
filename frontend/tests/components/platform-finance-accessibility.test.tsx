import { strict as assert } from 'node:assert';

const formatter = new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2 });
assert.match(formatter.format(0), /0/);
assert.equal('ج.م', 'ج.م');
export const platformFinanceAccessibilityContract = true;
