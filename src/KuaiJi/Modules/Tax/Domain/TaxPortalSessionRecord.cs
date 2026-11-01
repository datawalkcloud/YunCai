namespace KuaiJi.Modules.Tax.Domain
{
    /// <summary>
    /// 税局登录态的持久化记录：按「账套 + 用户」各存一行，密文由应用层加密后写入。
    /// 登录态放在数据库里，重建容器、换服务器只要连同一个库就能恢复，不再依赖本机文件。
    /// </summary>
    public class TaxPortalSessionRecord
    {
        private TaxPortalSessionRecord()
        {
        }

        private TaxPortalSessionRecord(
            Guid accountSetId,
            string userId,
            string payloadCipher,
            DateTimeOffset savedAt,
            DateTimeOffset updatedAt)
        {
            AccountSetId = accountSetId;
            UserId = userId;
            PayloadCipher = payloadCipher;
            SavedAt = savedAt;
            UpdatedAt = updatedAt;
        }

        public Guid AccountSetId { get; private set; }

        /// <summary>登录用户标识；同一账套下不同用户的税局会话互不复用。</summary>
        public string UserId { get; private set; } = null!;

        /// <summary>加密后的 storage state（Cookie + localStorage）与登录落地页。</summary>
        public string PayloadCipher { get; private set; } = null!;

        /// <summary>登录态在税局侧生效的时间，与 <see cref="UpdatedAt"/> 一起用于排查。</summary>
        public DateTimeOffset SavedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static TaxPortalSessionRecord Create(
            Guid accountSetId,
            string userId,
            string payloadCipher,
            DateTimeOffset savedAt)
        {
            return new TaxPortalSessionRecord(accountSetId, userId, payloadCipher, savedAt, savedAt);
        }

        public void Refresh(string payloadCipher, DateTimeOffset savedAt)
        {
            PayloadCipher = payloadCipher;
            SavedAt = savedAt;
            UpdatedAt = savedAt;
        }
    }
}
