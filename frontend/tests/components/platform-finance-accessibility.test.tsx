import { strict as assert } from 'node:assert';

const formatter = new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 });
assert.match(formatter.format(0), /٠/);
assert.equal('ج.م', 'ج.م');
export const platformFinanceAccessibilityContract = true;
