namespace YunCai.Modules.Archives.Domain
{
    /// <summary>档案文件状态。</summary>
    public enum ArchiveFileStatus
    {
        /// <summary>未审核。</summary>
        Unaudited = 1,

        /// <summary>已审核。</summary>
        Audited = 2,
    }

    /// <summary>
    /// 会计电子档案：凭证附件、银行回单、发票等原始凭证影像的归档管理。
    /// 文件内容直接入库存档（单账套总占用与免费版口径一致，由服务层限额）。
    /// </summary>
    public class ArchiveItem
    {
        /// <summary>文件名称最大长度。</summary>
        public const int MaxFileNameLength = 200;

        /// <summary>分类路径最大长度（分类树可有多层，路径由各节点名称用 &gt; 拼接）。</summary>
        public const int MaxCategoryPathLength = 500;

        /// <summary>单文件大小上限（10 MB，与免费版额度一致）。</summary>
        public const long MaxFileBytes = 10L * 1024 * 1024;

        private ArchiveItem()
        {
        }

        private ArchiveItem(
            Guid id,
            Guid accountSetId,
            string fileName,
            string category,
            string docType,
            byte[]? content,
            string? relatedDoc,
            string? relatedNo,
            string? voucherText,
            string? matter,
            string? checkStatus,
            string uploader,
            string source,
            bool isPaper)
        {
            Id = id;
            AccountSetId = accountSetId;
            FileName = fileName;
            Category = category;
            DocType = docType;
            Content = content;
            FileSize = content?.LongLength ?? 0;
            RelatedDoc = relatedDoc;
            RelatedNo = relatedNo;
            VoucherText = voucherText;
            Matter = matter;
            CheckStatus = checkStatus;
            Status = ArchiveFileStatus.Unaudited;
            IsPaper = isPaper;
            Uploader = uploader;
            Source = source;
            UploadedAt = DateTime.UtcNow;
        }

        /// <summary>档案标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>文件名称。</summary>
        public string FileName { get; private set; } = null!;

        /// <summary>文件类别（凭证附件 / 银行回单 / 发票…）。</summary>
        public string Category { get; private set; } = null!;

        /// <summary>单据类型。</summary>
        public string DocType { get; private set; } = null!;

        /// <summary>文件内容（入库存储），可为空（仅登记元数据）。</summary>
        public byte[]? Content { get; private set; }

        /// <summary>文件大小（字节）。</summary>
        public long FileSize { get; private set; }

        /// <summary>关联单据（银行日记账 / 现金日记账…），可为空。</summary>
        public string? RelatedDoc { get; private set; }

        /// <summary>关联单号，可为空。</summary>
        public string? RelatedNo { get; private set; }

        /// <summary>关联凭证文本（如 2026年9月 记-1），可为空。</summary>
        public string? VoucherText { get; private set; }

        /// <summary>事项说明，可为空。</summary>
        public string? Matter { get; private set; }

        /// <summary>校验状态说明，可为空。</summary>
        public string? CheckStatus { get; private set; }

        /// <summary>文件状态。</summary>
        public ArchiveFileStatus Status { get; private set; }

        /// <summary>上传人。</summary>
        public string Uploader { get; private set; } = null!;

        /// <summary>上传方式 / 来源（财务系统 / 银行回单…）。</summary>
        public string Source { get; private set; } = null!;

        /// <summary>上传时间。</summary>
        public DateTime UploadedAt { get; private set; }

        /// <summary>是否已归档。</summary>
        public bool IsArchived { get; private set; }

        /// <summary>是否标记为纸质档案（原件在纸面上，系统只留存影像）。</summary>
        public bool IsPaper { get; private set; }

        /// <summary>归档时间。</summary>
        public DateTime? ArchivedAt { get; private set; }

        /// <summary>归档人。</summary>
        public string? ArchivedBy { get; private set; }

        /// <summary>新建电子档案。</summary>
        public static ArchiveItem Create(
            Guid accountSetId,
            string fileName,
            string category,
            string docType,
            byte[]? content,
            string? relatedDoc,
            string? relatedNo,
            string? voucherText,
            string? matter,
            string? checkStatus,
            string uploader,
            string source,
            bool isPaper = false)
        {
            GuardFileName(fileName);
            if (content is { LongLength: > MaxFileBytes })
            {
                throw new ArgumentException("单个文件不能超过 10 MB。");
            }

            if (string.IsNullOrWhiteSpace(uploader))
            {
                throw new ArgumentException("上传人不能为空。");
            }

            return new ArchiveItem(
                Guid.NewGuid(),
                accountSetId,
                fileName.Trim(),
                Normalize(category) ?? string.Empty,
                Normalize(docType) ?? string.Empty,
                content,
                Normalize(relatedDoc),
                Normalize(relatedNo),
                Normalize(voucherText),
                Normalize(matter),
                Normalize(checkStatus),
                uploader.Trim(),
                Normalize(source) ?? string.Empty,
                isPaper);
        }

        /// <summary>修改档案元数据（不更新文件内容）。</summary>
        public void Update(
            string fileName,
            string category,
            string docType,
            string? relatedDoc,
            string? relatedNo,
            string? voucherText,
            string? matter,
            string? checkStatus)
        {
            GuardFileName(fileName);
            FileName = fileName.Trim();
            Category = Normalize(category) ?? string.Empty;
            DocType = Normalize(docType) ?? string.Empty;
            RelatedDoc = Normalize(relatedDoc);
            RelatedNo = Normalize(relatedNo);
            VoucherText = Normalize(voucherText);
            Matter = Normalize(matter);
            CheckStatus = Normalize(checkStatus);
        }

        /// <summary>审核 / 取消审核。</summary>
        public void SetAudited(bool audited) => Status = audited ? ArchiveFileStatus.Audited : ArchiveFileStatus.Unaudited;

        /// <summary>分类树节点重命名 / 移动后同步档案的分类路径。</summary>
        public void SetCategory(string category) => Category = Normalize(category) ?? string.Empty;

        /// <summary>标记 / 取消标记为纸质档案。</summary>
        public void SetPaper(bool isPaper) => IsPaper = isPaper;

        /// <summary>关联 / 取消关联凭证（voucherText 为空表示取消关联）。</summary>
        public void SetVoucherText(string? voucherText) => VoucherText = Normalize(voucherText);

        /// <summary>归档（记录归档人与时间）；重复归档忽略。</summary>
        public void Archive(string operatorName)
        {
            if (IsArchived)
            {
                return;
            }

            IsArchived = true;
            ArchivedAt = DateTime.UtcNow;
            ArchivedBy = operatorName.Trim();
        }

        private static void GuardFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("文件名称不能为空。");
            }

            if (fileName.Trim().Length > MaxFileNameLength)
            {
                throw new ArgumentException($"文件名称不能超过 {MaxFileNameLength} 个字符。", nameof(fileName));
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
