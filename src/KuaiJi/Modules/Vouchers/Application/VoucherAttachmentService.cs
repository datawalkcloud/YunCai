using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证附件应用服务：上传、列举、下载、删除原始单据影像。
    /// 附件按凭证归属，操作前先校验凭证存在且当前处于可改状态（草稿）。
    /// </summary>
    public sealed class VoucherAttachmentService
    {
        /// <summary>单个附件上限 10 MB，与控制器上传上限保持一致。</summary>
        public const long MaxAttachmentBytes = 10 * 1024 * 1024;

        private readonly IVoucherRepository _voucherRepository;
        private readonly IVoucherAttachmentRepository _attachmentRepository;

        public VoucherAttachmentService(IVoucherRepository voucherRepository, IVoucherAttachmentRepository attachmentRepository)
        {
            _voucherRepository = voucherRepository;
            _attachmentRepository = attachmentRepository;
        }

        /// <summary>列出某张凭证的附件元数据。</summary>
        public async Task<IReadOnlyList<VoucherAttachmentDto>> ListAsync(Guid accountSetId, Guid voucherId, CancellationToken cancellationToken = default)
        {
            await EnsureVoucherExistsAsync(accountSetId, voucherId, cancellationToken);
            var items = await _attachmentRepository.ListByVoucherAsync(voucherId, cancellationToken);
            return items.Select(VoucherAttachmentDto.FromDomain).ToList();
        }

        /// <summary>上传一个附件；返回落库后的元数据。</summary>
        public async Task<VoucherAttachmentDto> UploadAsync(
            Guid accountSetId,
            Guid voucherId,
            string fileName,
            string? contentType,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            var voucher = await EnsureVoucherExistsAsync(accountSetId, voucherId, cancellationToken);

            if (voucher.Status != VoucherStatus.Draft)
            {
                throw new VoucherException("凭证已审核或已过账，不能再上传附件。");
            }

            if (content is null || content.Length == 0)
            {
                throw new VoucherException("附件内容为空，请重新选择文件。");
            }

            if (content.LongLength > MaxAttachmentBytes)
            {
                throw new VoucherException($"附件「{fileName}」超过 10 MB，请压缩后再上传。");
            }

            VoucherAttachment attachment;
            try
            {
                attachment = VoucherAttachment.Create(voucherId, fileName, contentType, content);
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }

            await _attachmentRepository.AddAsync(attachment, cancellationToken);
            await _attachmentRepository.SaveChangesAsync(cancellationToken);

            return VoucherAttachmentDto.FromDomain(
                new VoucherAttachmentInfo(attachment.Id, attachment.VoucherId, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAt));
        }

        /// <summary>下载一个附件，返回文件字节与下载所需的元信息；不存在时返回 null。</summary>
        public async Task<(VoucherAttachment Attachment, byte[] Content)?> DownloadAsync(
            Guid accountSetId,
            Guid voucherId,
            Guid attachmentId,
            CancellationToken cancellationToken = default)
        {
            await EnsureVoucherExistsAsync(accountSetId, voucherId, cancellationToken);
            var attachment = await _attachmentRepository.GetAsync(voucherId, attachmentId, cancellationToken);
            return attachment is null ? null : (attachment, attachment.Content);
        }

        /// <summary>删除一个附件；不存在时静默返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid voucherId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var voucher = await EnsureVoucherExistsAsync(accountSetId, voucherId, cancellationToken);
            if (voucher.Status != VoucherStatus.Draft)
            {
                throw new VoucherException("凭证已审核或已过账，不能删除附件。");
            }

            var attachment = await _attachmentRepository.GetAsync(voucherId, attachmentId, cancellationToken);
            if (attachment is null)
            {
                return false;
            }

            _attachmentRepository.Remove(attachment);
            await _attachmentRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task<Voucher> EnsureVoucherExistsAsync(Guid accountSetId, Guid voucherId, CancellationToken cancellationToken)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, voucherId, cancellationToken);
            if (voucher is null)
            {
                throw new VoucherException("凭证不存在或不属于当前账套。");
            }

            return voucher;
        }
    }
}
