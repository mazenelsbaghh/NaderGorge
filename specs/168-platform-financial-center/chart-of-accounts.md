# Seeded EGP chart of accounts

| Code | Account | Type / role |
|---:|---|---|
| 1000 | خزينة ومحافظ المنصة | Asset / Treasury |
| 1100 | التزام أرصدة الطلبة العامة | Liability / GeneralStudentLiability |
| 1110 | التزام أرصدة الطلبة المرتبطة بمدرس | Liability / TeacherStudentLiability |
| 2000 | مستحقات المدرسين | Liability / TeacherPayable |
| 2100 | مستحقات الموردين | Liability / SupplierPayable |
| 4000 | إيرادات المنصة | Revenue / PlatformRevenue |
| 4100 | مردودات واستردادات | Contra-revenue / Refunds |
| 5000 | مصروفات تشغيلية | Expense / OperatingExpense |
| 5100 | مصروفات رواتب | Expense / PayrollExpense |
| 9990 | حساب تسوية مؤقت | Equity / OpeningSuspense |

Each active digital wallet gets a separate asset account under the treasury role (`W{walletId:N}`) while cashboxes use the seeded `1000` account. Amounts are stored in EGP with two decimal places.
