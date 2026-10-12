using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 报表会计期间解析的共享口径：显式给定 year/month 时校验后采用；
    /// 否则取最近有凭证的月份，无凭证时回退到账套启用期间。
    /// </summary>
    public static class ReportPeriodResolver
    {
        public static (int Year, int Month) Resolve(
            AccountSet accountSet,
            IReadOnlyList<Voucher> vouchers,
            int? year,
            int? month)
        {
            if (year is { } y && month is { } m)
            {
                if (m < 1 || m > 12)
                {
                    throw new ReportException("会计期间的月份必须在 1 到 12 之间。");
                }

                if (y < 1900 || y > 9999)
                {
                    throw new ReportException("会计期间的年份不合法。");
                }

                return (y, m);
            }

            if (vouchers.Count > 0)
            {
                var latest = vouchers.Max(v => v.Date);
                return (latest.Year, latest.Month);
            }

            return (accountSet.StartYear, accountSet.StartMonth);
        }
    }
}
