namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>
    /// 税局实名密码的存储保护：入库前加密，交给助手时解密。
    /// 它保证的是「数据库里看不到明文」，不保证同时拿到库和签名密钥的人也读不到——
    /// 后者要靠密钥管理，不在应用层能解决的范围内。
    /// </summary>
    public interface ITaxCredentialProtector
    {
        /// <summary>把明文密码封装成可入库的密文。</summary>
        string Encrypt(string plaintext);

        /// <summary>
        /// 还原密文；密文缺失、版本不认识或密钥已更换导致解不开时返回 null，
        /// 让上层按「未填写税局密码」处理，而不是让整个账套接口报错。
        /// </summary>
        string? Decrypt(string? cipher);
    }
}
