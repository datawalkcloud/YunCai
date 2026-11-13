using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Archives.Domain;
using YunCai.Modules.Vouchers.Application;

namespace YunCai.Modules.Archives.Application
{
    /// <summary>电子档案相关业务错误。</summary>
    public sealed class ArchiveException : Exception
    {
        public ArchiveException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 会计电子档案应用服务：档案登记（可含文件内容）/ 修改 / 删除 / 审核 / 归档 / 下载 / 空间统计。
    /// 免费版空间总量 10 MB（与 <see cref="ArchiveItem.MaxFileBytes"/> 单文件上限一致）。
    /// </summary>
    public sealed class ArchiveService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IArchiveRepository _archiveRepository;

        public ArchiveService(
            IAccountSetRepository accountSetRepository,
            IArchiveRepository archiveRepository,
            VoucherService voucherService)
        {
            _accountSetRepository = accountSetRepository;
            _archiveRepository = archiveRepository;
            _voucherService = voucherService;
        }

        private readonly VoucherService _voucherService;

        /// <summary>
        /// 由档案生成凭证：选中档案合并为一张「借 A 科目 / 贷 B 科目」凭证，
        /// 附件数 = 档案数，生成后把凭证字号回写到各档案。
        /// </summary>
        public async Task<string> GenerateVoucherAsync(Guid accountSetId, GenerateVoucherFromArchivesRequest request, string preparer, CancellationToken cancellationToken = default)
        {
            if (request.Ids.Count == 0)
            {
                throw new ArchiveException("请先勾选要生成凭证的档案。");
            }

            if (request.Amount <= 0m)
            {
                throw new ArchiveException("凭证金额需大于 0。");
            }

            if (request.DebitAccountId == Guid.Empty || request.CreditAccountId == Guid.Empty)
            {
                throw new ArchiveException("请选择借方与贷方科目。");
            }

            if (request.DebitAccountId == request.CreditAccountId)
            {
                throw new ArchiveException("借方与贷方科目不能相同。");
            }

            var archives = new List<ArchiveItem>();
            foreach (var id in request.Ids)
            {
                var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (item is not null)
                {
                    archives.Add(item);
                }
            }

            if (archives.Count == 0)
            {
                throw new ArchiveException("选中的档案不存在。");
            }

            var summary = string.IsNullOrWhiteSpace(request.Summary)
                ? (archives[0].Matter ?? archives[0].FileName)
                : request.Summary.Trim();

            var voucher = await _voucherService.CreateAsync(accountSetId, preparer, new CreateVoucherRequest
            {
                Word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim(),
                Date = request.Date,
                AttachmentCount = archives.Count,
                Remark = $"由电子档案生成（{archives.Count} 份）",
                Entries =
                [
                    new CreateVoucherEntryRequest { Summary = summary, AccountId = request.DebitAccountId, Debit = request.Amount, Credit = 0m },
                    new CreateVoucherEntryRequest { Summary = summary, AccountId = request.CreditAccountId, Debit = 0m, Credit = request.Amount },
                ],
            }, cancellationToken);

            var voucherText = voucher.VoucherNo;
            foreach (var item in archives)
            {
                item.SetVoucherText(voucherText);
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return voucherText;
        }

        /// <summary>获取未归档档案列表（元数据投影）。</summary>
        public async Task<IReadOnlyList<ArchiveItemDto>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var items = await _archiveRepository.GetListAsync(accountSetId, archived: false, cancellationToken);
            return items.Select(ArchiveItemDto.FromDomain).ToList();
        }

        /// <summary>获取已归档档案列表。</summary>
        public async Task<IReadOnlyList<ArchiveItemDto>> GetArchivedListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var items = await _archiveRepository.GetListAsync(accountSetId, archived: true, cancellationToken);
            return items.Select(ArchiveItemDto.FromDomain).ToList();
        }

        /// <summary>获取已用空间。</summary>
        public async Task<ArchiveUsageDto> GetUsageAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var used = await _archiveRepository.GetTotalSizeAsync(accountSetId, cancellationToken);
            return new ArchiveUsageDto { UsedBytes = used };
        }

        /// <summary>新建档案（可含文件内容；总量超限拒绝）。</summary>
        public async Task<ArchiveItemDto> CreateAsync(Guid accountSetId, SaveArchiveRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var content = DecodeContent(request.FileBase64);
            if (content is not null)
            {
                var used = await _archiveRepository.GetTotalSizeAsync(accountSetId, cancellationToken);
                if (used + content.LongLength > ArchiveItem.MaxFileBytes)
                {
                    throw new ArchiveException("归档空间不足（免费版总量 10 MB），请升级扩容或清理档案。");
                }
            }

            ArchiveItem item;
            try
            {
                item = ArchiveItem.Create(
                    accountSetId,
                    request.FileName,
                    request.Category,
                    request.DocType,
                    content,
                    request.RelatedDoc,
                    request.RelatedNo,
                    request.VoucherText,
                    request.Matter,
                    request.CheckStatus,
                    request.Uploader,
                    request.Source,
                    request.IsPaper);
            }
            catch (ArgumentException ex)
            {
                throw new ArchiveException(ex.Message);
            }

            await _archiveRepository.AddAsync(item, cancellationToken);
            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return ArchiveItemDto.FromDomain(item);
        }

        /// <summary>修改档案元数据；不存在时返回 null。</summary>
        public async Task<ArchiveItemDto?> UpdateAsync(Guid accountSetId, Guid id, SaveArchiveRequest request, CancellationToken cancellationToken = default)
        {
            var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            try
            {
                item.Update(
                    request.FileName,
                    request.Category,
                    request.DocType,
                    request.RelatedDoc,
                    request.RelatedNo,
                    request.VoucherText,
                    request.Matter,
                    request.CheckStatus);
            }
            catch (ArgumentException ex)
            {
                throw new ArchiveException(ex.Message);
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return ArchiveItemDto.FromDomain(item);
        }

        /// <summary>删除档案；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return false;
            }

            _archiveRepository.Remove(item);
            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>批量审核 / 取消审核。返回实际处理条数。</summary>
        public async Task<int> SetAuditedAsync(Guid accountSetId, AuditArchiveRequest request, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var id in request.Ids)
            {
                var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (item is null)
                {
                    continue;
                }

                item.SetAudited(request.Audited);
                count++;
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>批量标记 / 取消标记为纸质档案。返回实际处理条数。</summary>
        public async Task<int> SetPaperAsync(Guid accountSetId, PaperArchiveRequest request, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var id in request.Ids)
            {
                var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (item is null)
                {
                    continue;
                }

                item.SetPaper(request.IsPaper);
                count++;
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>批量关联 / 取消关联凭证（voucherText 为空即取消关联）。返回实际处理条数。</summary>
        public async Task<int> LinkVoucherAsync(Guid accountSetId, LinkVoucherRequest request, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var id in request.Ids)
            {
                var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (item is null)
                {
                    continue;
                }

                item.SetVoucherText(request.VoucherText);
                count++;
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>批量归档（记录归档人）。返回实际归档条数。</summary>
        public async Task<int> ArchiveAsync(Guid accountSetId, ArchiveIdsRequest request, string operatorName, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var id in request.Ids)
            {
                var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (item is null)
                {
                    continue;
                }

                item.Archive(operatorName);
                count++;
            }

            await _archiveRepository.SaveChangesAsync(cancellationToken);
            return count;
        }

        /// <summary>下载档案：返回文件名与内容；档案或内容不存在时返回 null。</summary>
        public async Task<(string FileName, byte[] Content)?> DownloadAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var item = await _archiveRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            var content = await _archiveRepository.GetContentAsync(accountSetId, id, cancellationToken);
            if (content is null || content.Length == 0)
            {
                return null;
            }

            return (item.FileName, content);
        }

        private static byte[]? DecodeContent(string? fileBase64)
        {
            if (string.IsNullOrWhiteSpace(fileBase64))
            {
                return null;
            }

            try
            {
                return Convert.FromBase64String(fileBase64);
            }
            catch (FormatException)
            {
                throw new ArchiveException("文件内容不是合法的 Base64 数据。");
            }
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new ArchiveException("指定的账套不存在。");
            }
        }
    }
}
