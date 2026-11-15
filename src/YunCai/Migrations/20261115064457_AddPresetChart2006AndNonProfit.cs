using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddPresetChart2006AndNonProfit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "preset_accounts",
                columns: new[] { "id", "category", "code", "direction", "name", "order_no", "parent_code", "standard" },
                values: new object[,]
                {
                    { 468, 1, "1001", 1, "库存现金", 1, null, 4 },
                    { 469, 1, "1002", 1, "银行存款", 2, null, 4 },
                    { 470, 1, "1012", 1, "其他货币资金", 3, null, 4 },
                    { 471, 1, "101201", 1, "外埠存款", 4, "1012", 4 },
                    { 472, 1, "101202", 1, "银行本票存款", 5, "1012", 4 },
                    { 473, 1, "101203", 1, "银行汇票存款", 6, "1012", 4 },
                    { 474, 1, "101204", 1, "信用卡存款", 7, "1012", 4 },
                    { 475, 1, "101205", 1, "信用证保证金存款", 8, "1012", 4 },
                    { 476, 1, "101206", 1, "存出投资款", 9, "1012", 4 },
                    { 477, 1, "1101", 1, "交易性金融资产", 10, null, 4 },
                    { 478, 1, "110101", 1, "本金", 11, "1101", 4 },
                    { 479, 1, "11010101", 1, "股票", 12, "110101", 4 },
                    { 480, 1, "11010102", 1, "债券", 13, "110101", 4 },
                    { 481, 1, "11010103", 1, "基金", 14, "110101", 4 },
                    { 482, 1, "11010104", 1, "权证", 15, "110101", 4 },
                    { 483, 1, "11010199", 1, "其他", 16, "110101", 4 },
                    { 484, 1, "110102", 1, "公允价值变动", 17, "1101", 4 },
                    { 485, 1, "11010201", 1, "股票", 18, "110102", 4 },
                    { 486, 1, "11010202", 1, "债券", 19, "110102", 4 },
                    { 487, 1, "11010203", 1, "基金", 20, "110102", 4 },
                    { 488, 1, "11010204", 1, "权证", 21, "110102", 4 },
                    { 489, 1, "11010299", 1, "其他", 22, "110102", 4 },
                    { 490, 1, "1121", 1, "应收票据", 23, null, 4 },
                    { 491, 1, "1122", 1, "应收账款", 24, null, 4 },
                    { 492, 1, "1123", 1, "预付账款", 25, null, 4 },
                    { 493, 1, "1131", 1, "应收股利", 26, null, 4 },
                    { 494, 1, "1132", 1, "应收利息", 27, null, 4 },
                    { 495, 1, "1221", 1, "其他应收款", 28, null, 4 },
                    { 496, 1, "1231", 2, "坏账准备", 29, null, 4 },
                    { 497, 1, "123101", 2, "应收账款坏账准备", 30, "1231", 4 },
                    { 498, 1, "123102", 2, "其他应收款坏账准备", 31, "1231", 4 },
                    { 499, 1, "1321", 1, "受托代销商品", 32, null, 4 },
                    { 500, 1, "1401", 1, "材料采购", 33, null, 4 },
                    { 501, 1, "1402", 1, "在途物资", 34, null, 4 },
                    { 502, 1, "1403", 1, "原材料", 35, null, 4 },
                    { 503, 1, "1404", 1, "材料成本差异", 36, null, 4 },
                    { 504, 1, "1405", 1, "库存商品", 37, null, 4 },
                    { 505, 1, "1406", 1, "发出商品", 38, null, 4 },
                    { 506, 1, "1407", 2, "商品进销差价", 39, null, 4 },
                    { 507, 1, "1408", 1, "委托加工物资", 40, null, 4 },
                    { 508, 1, "1411", 1, "周转材料", 41, null, 4 },
                    { 509, 1, "1471", 2, "存货跌价准备", 42, null, 4 },
                    { 510, 1, "1501", 1, "持有至到期投资", 43, null, 4 },
                    { 511, 1, "150101", 1, "投资成本", 44, "1501", 4 },
                    { 512, 1, "150102", 1, "溢折价", 45, "1501", 4 },
                    { 513, 1, "150103", 1, "应计利息", 46, "1501", 4 },
                    { 514, 1, "1502", 2, "持有至到期投资减值准备", 47, null, 4 },
                    { 515, 1, "1503", 1, "可供出售金融资产", 48, null, 4 },
                    { 516, 1, "1511", 1, "长期股权投资", 49, null, 4 },
                    { 517, 1, "151101", 1, "投资成本", 50, "1511", 4 },
                    { 518, 1, "151102", 1, "损益调整", 51, "1511", 4 },
                    { 519, 1, "151103", 1, "所有者权益其他变动", 52, "1511", 4 },
                    { 520, 1, "1512", 2, "长期股权投资减值准备", 53, null, 4 },
                    { 521, 1, "1521", 1, "投资性房地产", 54, null, 4 },
                    { 522, 1, "152101", 1, "成本", 55, "1521", 4 },
                    { 523, 1, "152102", 1, "公允价值变动", 56, "1521", 4 },
                    { 524, 1, "1531", 1, "长期应收款", 57, null, 4 },
                    { 525, 1, "1532", 1, "未实现融资收益", 58, null, 4 },
                    { 526, 1, "1601", 1, "固定资产", 59, null, 4 },
                    { 527, 1, "1602", 2, "累计折旧", 60, null, 4 },
                    { 528, 1, "1603", 2, "固定资产减值准备", 61, null, 4 },
                    { 529, 1, "1604", 1, "在建工程", 62, null, 4 },
                    { 530, 1, "160401", 1, "建筑工程", 63, "1604", 4 },
                    { 531, 1, "160402", 1, "安装工程", 64, "1604", 4 },
                    { 532, 1, "160403", 1, "在安装设备", 65, "1604", 4 },
                    { 533, 1, "160404", 1, "待摊支出", 66, "1604", 4 },
                    { 534, 1, "1605", 1, "工程物资", 67, null, 4 },
                    { 535, 1, "160501", 1, "专用材料", 68, "1605", 4 },
                    { 536, 1, "160502", 1, "专用设备", 69, "1605", 4 },
                    { 537, 1, "160503", 1, "预付大型设备款", 70, "1605", 4 },
                    { 538, 1, "160504", 1, "为生产准备的工具及器具", 71, "1605", 4 },
                    { 539, 1, "1606", 1, "固定资产清理", 72, null, 4 },
                    { 540, 1, "1621", 1, "生产性生物资产", 73, null, 4 },
                    { 541, 1, "1622", 2, "生产性生物资产累计折旧", 74, null, 4 },
                    { 542, 1, "1701", 1, "无形资产", 75, null, 4 },
                    { 543, 1, "1702", 2, "累计摊销", 76, null, 4 },
                    { 544, 1, "1703", 2, "无形资产减值准备", 77, null, 4 },
                    { 545, 1, "1711", 1, "商誉", 78, null, 4 },
                    { 546, 1, "1801", 1, "长期待摊费用", 79, null, 4 },
                    { 547, 1, "1811", 1, "递延所得税资产", 80, null, 4 },
                    { 548, 1, "1901", 1, "待处理财产损溢", 81, null, 4 },
                    { 549, 2, "2001", 2, "短期借款", 82, null, 4 },
                    { 550, 2, "2101", 2, "交易性金融负债", 83, null, 4 },
                    { 551, 2, "210101", 2, "本金", 84, "2101", 4 },
                    { 552, 2, "210102", 2, "公允价值变动", 85, "2101", 4 },
                    { 553, 2, "2201", 2, "应付票据", 86, null, 4 },
                    { 554, 2, "2202", 2, "应付账款", 87, null, 4 },
                    { 555, 2, "2203", 2, "预收账款", 88, null, 4 },
                    { 556, 2, "2211", 2, "应付职工薪酬", 89, null, 4 },
                    { 557, 2, "221101", 2, "工资", 90, "2211", 4 },
                    { 558, 2, "221102", 2, "职工福利", 91, "2211", 4 },
                    { 559, 2, "221103", 2, "社会保险费", 92, "2211", 4 },
                    { 560, 2, "221104", 2, "住房公积金", 93, "2211", 4 },
                    { 561, 2, "221105", 2, "工会经费", 94, "2211", 4 },
                    { 562, 2, "221106", 2, "职工教育经费", 95, "2211", 4 },
                    { 563, 2, "221107", 2, "解除职工劳动关系补偿", 96, "2211", 4 },
                    { 564, 2, "221108", 2, "奖金", 97, "2211", 4 },
                    { 565, 2, "2221", 2, "应交税费", 98, null, 4 },
                    { 566, 2, "222101", 2, "应交增值税", 99, "2221", 4 },
                    { 567, 2, "22210101", 1, "进项税额", 100, "222101", 4 },
                    { 568, 2, "22210102", 1, "销项税额抵减", 101, "222101", 4 },
                    { 569, 2, "22210103", 1, "已交税金", 102, "222101", 4 },
                    { 570, 2, "22210104", 1, "转出未交增值税", 103, "222101", 4 },
                    { 571, 2, "22210105", 1, "减免税款", 104, "222101", 4 },
                    { 572, 2, "22210106", 1, "出口抵减内销产品应纳税额", 105, "222101", 4 },
                    { 573, 2, "22210107", 2, "销项税额", 106, "222101", 4 },
                    { 574, 2, "22210108", 2, "出口退税", 107, "222101", 4 },
                    { 575, 2, "22210109", 2, "进项税额转出", 108, "222101", 4 },
                    { 576, 2, "22210110", 2, "转出多交增值税", 109, "222101", 4 },
                    { 577, 2, "222102", 2, "未交增值税", 110, "2221", 4 },
                    { 578, 2, "222103", 1, "预交增值税", 111, "2221", 4 },
                    { 579, 2, "222104", 1, "待抵扣进项税额", 112, "2221", 4 },
                    { 580, 2, "222105", 1, "待认证进项税额", 113, "2221", 4 },
                    { 581, 2, "222106", 2, "待转销项税额", 114, "2221", 4 },
                    { 582, 2, "222107", 1, "增值税留抵税额", 115, "2221", 4 },
                    { 583, 2, "222108", 2, "简易计税", 116, "2221", 4 },
                    { 584, 2, "222109", 2, "转让金融商品应交增值税", 117, "2221", 4 },
                    { 585, 2, "222110", 2, "代扣代交增值税", 118, "2221", 4 },
                    { 586, 2, "222111", 2, "应交所得税", 119, "2221", 4 },
                    { 587, 2, "222112", 2, "应交个人所得税", 120, "2221", 4 },
                    { 588, 2, "222113", 2, "应交教育费附加", 121, "2221", 4 },
                    { 589, 2, "222114", 2, "应交城市维护建设税", 122, "2221", 4 },
                    { 590, 2, "222115", 2, "应交地方教育费附加", 123, "2221", 4 },
                    { 591, 2, "222116", 2, "应交土地增值税", 124, "2221", 4 },
                    { 592, 2, "222117", 2, "应交资源税", 125, "2221", 4 },
                    { 593, 2, "222118", 2, "应交房产税", 126, "2221", 4 },
                    { 594, 2, "222119", 2, "应交土地使用税", 127, "2221", 4 },
                    { 595, 2, "222120", 2, "应交车船使用税", 128, "2221", 4 },
                    { 596, 2, "222121", 2, "应交消费税", 129, "2221", 4 },
                    { 597, 2, "222122", 2, "应交矿产资源补偿费", 130, "2221", 4 },
                    { 598, 2, "222123", 2, "应交印花税", 131, "2221", 4 },
                    { 599, 2, "222124", 2, "应交关税", 132, "2221", 4 },
                    { 600, 2, "2231", 2, "应付股利", 133, null, 4 },
                    { 601, 2, "2232", 2, "应付利息", 134, null, 4 },
                    { 602, 2, "2241", 2, "其他应付款", 135, null, 4 },
                    { 603, 2, "2314", 2, "受托代湮商品款", 136, null, 4 },
                    { 604, 2, "2401", 2, "递延收益", 137, null, 4 },
                    { 605, 2, "2501", 2, "长期借款", 138, null, 4 },
                    { 606, 2, "250101", 2, "本金", 139, "2501", 4 },
                    { 607, 2, "250102", 2, "利息调整", 140, "2501", 4 },
                    { 608, 2, "2502", 2, "应付债券", 141, null, 4 },
                    { 609, 2, "250201", 2, "债券面值", 142, "2502", 4 },
                    { 610, 2, "250202", 2, "利息调整", 143, "2502", 4 },
                    { 611, 2, "250203", 2, "应计利息", 144, "2502", 4 },
                    { 612, 2, "2701", 2, "长期应付款", 145, null, 4 },
                    { 613, 2, "2702", 2, "未确认融资费用", 146, null, 4 },
                    { 614, 2, "2711", 2, "专项应付款", 147, null, 4 },
                    { 615, 2, "2801", 2, "预计负债", 148, null, 4 },
                    { 616, 2, "2901", 2, "递延所得税负债", 149, null, 4 },
                    { 617, 1, "3101", 1, "衍生工具", 150, null, 4 },
                    { 618, 1, "3201", 1, "套期工具", 151, null, 4 },
                    { 619, 1, "3202", 1, "被套期项目", 152, null, 4 },
                    { 620, 3, "4001", 2, "实收资本", 153, null, 4 },
                    { 621, 3, "4002", 2, "资本公积", 154, null, 4 },
                    { 622, 3, "400201", 2, "资本溢价", 155, "4002", 4 },
                    { 623, 3, "400202", 2, "股本溢价", 156, "4002", 4 },
                    { 624, 3, "400203", 2, "其他资本公积", 157, "4002", 4 },
                    { 625, 3, "4003", 2, "其他综合收益", 158, null, 4 },
                    { 626, 3, "4101", 2, "盈余公积", 159, null, 4 },
                    { 627, 3, "410101", 2, "法定盈余公积", 160, "4101", 4 },
                    { 628, 3, "410102", 2, "任意盈余公积", 161, "4101", 4 },
                    { 629, 3, "410103", 2, "法定公益金", 162, "4101", 4 },
                    { 630, 3, "410104", 2, "储备基金", 163, "4101", 4 },
                    { 631, 3, "410105", 2, "企业发展基金", 164, "4101", 4 },
                    { 632, 3, "410106", 2, "利润归还投资", 165, "4101", 4 },
                    { 633, 3, "4103", 2, "本年利润", 166, null, 4 },
                    { 634, 3, "4104", 2, "利润分配", 167, null, 4 },
                    { 635, 3, "410401", 1, "提取法定盈余公积", 168, "4104", 4 },
                    { 636, 3, "410402", 1, "提取任意盈余公积", 169, "4104", 4 },
                    { 637, 3, "410403", 1, "提取法定公益金", 170, "4104", 4 },
                    { 638, 3, "410404", 1, "应付现金股利或利润", 171, "4104", 4 },
                    { 639, 3, "410405", 1, "转作股本的利润", 172, "4104", 4 },
                    { 640, 3, "410406", 1, "盈余公积补亏", 173, "4104", 4 },
                    { 641, 3, "410407", 1, "提取储备基金", 174, "4104", 4 },
                    { 642, 3, "410408", 1, "提取企业发展基金", 175, "4104", 4 },
                    { 643, 3, "410409", 1, "提取职工奖励及福利基金", 176, "4104", 4 },
                    { 644, 3, "410410", 1, "利润归还投资", 177, "4104", 4 },
                    { 645, 3, "410411", 2, "未分配利润", 178, "4104", 4 },
                    { 646, 3, "4201", 1, "库存股", 179, null, 4 },
                    { 647, 4, "5001", 1, "生产成本", 180, null, 4 },
                    { 648, 4, "500101", 1, "基本生产成本", 181, "5001", 4 },
                    { 649, 4, "500102", 1, "辅助生产成本", 182, "5001", 4 },
                    { 650, 4, "5101", 1, "制造费用", 183, null, 4 },
                    { 651, 4, "5201", 1, "劳务成本", 184, null, 4 },
                    { 652, 4, "5301", 1, "研发支出", 185, null, 4 },
                    { 653, 4, "530101", 1, "费用化支出", 186, "5301", 4 },
                    { 654, 4, "530102", 1, "资本化支出", 187, "5301", 4 },
                    { 655, 5, "6001", 2, "主营业务收入", 188, null, 4 },
                    { 656, 5, "6051", 2, "其他业务收入", 189, null, 4 },
                    { 657, 5, "6101", 2, "公允价值变动损益", 190, null, 4 },
                    { 658, 5, "6111", 2, "投资收益", 191, null, 4 },
                    { 659, 5, "6301", 2, "营业外收入", 192, null, 4 },
                    { 660, 5, "6401", 1, "主营业务成本", 193, null, 4 },
                    { 661, 5, "6402", 1, "其他业务成本", 194, null, 4 },
                    { 662, 5, "6403", 1, "税金及附加", 195, null, 4 },
                    { 663, 5, "6601", 1, "销售费用", 196, null, 4 },
                    { 664, 5, "660101", 1, "办公费", 197, "6601", 4 },
                    { 665, 5, "660102", 1, "房租", 198, "6601", 4 },
                    { 666, 5, "660103", 1, "物业管理费", 199, "6601", 4 },
                    { 667, 5, "660104", 1, "水电费", 200, "6601", 4 },
                    { 668, 5, "660105", 1, "交际应酬费", 201, "6601", 4 },
                    { 669, 5, "660106", 1, "市内交通费", 202, "6601", 4 },
                    { 670, 5, "660107", 1, "差旅费", 203, "6601", 4 },
                    { 671, 5, "660108", 1, "补助", 204, "6601", 4 },
                    { 672, 5, "660109", 1, "通讯费", 205, "6601", 4 },
                    { 673, 5, "660110", 1, "工资", 206, "6601", 4 },
                    { 674, 5, "660111", 1, "佣金", 207, "6601", 4 },
                    { 675, 5, "660112", 1, "保险金", 208, "6601", 4 },
                    { 676, 5, "660113", 1, "福利费", 209, "6601", 4 },
                    { 677, 5, "660114", 1, "包装费", 210, "6601", 4 },
                    { 678, 5, "660115", 1, "广告和业务宣传费", 211, "6601", 4 },
                    { 679, 5, "660116", 1, "商品维修费", 212, "6601", 4 },
                    { 680, 5, "660117", 1, "预计产品质量保证损失", 213, "6601", 4 },
                    { 681, 5, "660118", 1, "运输费", 214, "6601", 4 },
                    { 682, 5, "660119", 1, "装卸费", 215, "6601", 4 },
                    { 683, 5, "660120", 1, "折旧费", 216, "6601", 4 },
                    { 684, 5, "660121", 1, "计提福利", 217, "6601", 4 },
                    { 685, 5, "660122", 1, "职工教育经费", 218, "6601", 4 },
                    { 686, 5, "660123", 1, "业务招待费", 219, "6601", 4 },
                    { 687, 5, "660124", 1, "劳务费", 220, "6601", 4 },
                    { 688, 5, "660199", 1, "其他", 221, "6601", 4 },
                    { 689, 5, "6602", 1, "管理费用", 222, null, 4 },
                    { 690, 5, "660201", 1, "办公费", 223, "6602", 4 },
                    { 691, 5, "660202", 1, "房租", 224, "6602", 4 },
                    { 692, 5, "660203", 1, "物业管理费", 225, "6602", 4 },
                    { 693, 5, "660204", 1, "水电费", 226, "6602", 4 },
                    { 694, 5, "660205", 1, "交际应酬费", 227, "6602", 4 },
                    { 695, 5, "660206", 1, "市内交通费", 228, "6602", 4 },
                    { 696, 5, "660207", 1, "差旅费", 229, "6602", 4 },
                    { 697, 5, "660208", 1, "通讯费", 230, "6602", 4 },
                    { 698, 5, "660209", 1, "工资", 231, "6602", 4 },
                    { 699, 5, "660210", 1, "保险金", 232, "6602", 4 },
                    { 700, 5, "660211", 1, "福利费", 233, "6602", 4 },
                    { 701, 5, "660212", 1, "工会经费", 234, "6602", 4 },
                    { 702, 5, "660213", 1, "董事会费", 235, "6602", 4 },
                    { 703, 5, "660214", 1, "聘请中介机构费", 236, "6602", 4 },
                    { 704, 5, "660215", 1, "咨询费", 237, "6602", 4 },
                    { 705, 5, "660216", 1, "诉讼费", 238, "6602", 4 },
                    { 706, 5, "660217", 1, "房产税", 239, "6602", 4 },
                    { 707, 5, "660218", 1, "车船使用税", 240, "6602", 4 },
                    { 708, 5, "660219", 1, "土地使用税", 241, "6602", 4 },
                    { 709, 5, "660220", 1, "印花税", 242, "6602", 4 },
                    { 710, 5, "660221", 1, "技术转让费", 243, "6602", 4 },
                    { 711, 5, "660222", 1, "矿产资源补偿费", 244, "6602", 4 },
                    { 712, 5, "660223", 1, "研究费用", 245, "6602", 4 },
                    { 713, 5, "660224", 1, "排污费", 246, "6602", 4 },
                    { 714, 5, "660225", 1, "折旧费", 247, "6602", 4 },
                    { 715, 5, "660226", 1, "计提福利", 248, "6602", 4 },
                    { 716, 5, "660227", 1, "职工教育经费", 249, "6602", 4 },
                    { 717, 5, "660228", 1, "存货盘亏或盘盈", 250, "6602", 4 },
                    { 718, 5, "660229", 1, "交通费", 251, "6602", 4 },
                    { 719, 5, "660230", 1, "运杂费", 252, "6602", 4 },
                    { 720, 5, "660231", 1, "修理费", 253, "6602", 4 },
                    { 721, 5, "660232", 1, "公积金", 254, "6602", 4 },
                    { 722, 5, "660233", 1, "社保", 255, "6602", 4 },
                    { 723, 5, "660234", 1, "汽车费用", 256, "6602", 4 },
                    { 724, 5, "660235", 1, "服务费", 257, "6602", 4 },
                    { 725, 5, "660236", 1, "无形资产摊销", 258, "6602", 4 },
                    { 726, 5, "660237", 1, "招聘费", 259, "6602", 4 },
                    { 727, 5, "660238", 1, "业务招待费", 260, "6602", 4 },
                    { 728, 5, "660299", 1, "其他", 261, "6602", 4 },
                    { 729, 5, "6603", 1, "财务费用", 262, null, 4 },
                    { 730, 5, "660301", 1, "汇兑损益", 263, "6603", 4 },
                    { 731, 5, "660302", 1, "利息", 264, "6603", 4 },
                    { 732, 5, "660303", 1, "手续费", 265, "6603", 4 },
                    { 733, 5, "660304", 1, "账户管理费", 266, "6603", 4 },
                    { 734, 5, "660399", 1, "其他", 267, "6603", 4 },
                    { 735, 5, "6701", 1, "资产减值损失", 268, null, 4 },
                    { 736, 5, "6711", 1, "营业外支出", 269, null, 4 },
                    { 737, 5, "6801", 1, "所得税", 270, null, 4 },
                    { 738, 5, "680101", 1, "当期所得税费用", 271, "6801", 4 },
                    { 739, 5, "680102", 1, "递延所得税费用", 272, "6801", 4 },
                    { 740, 5, "6901", 1, "以前年度损益调整", 273, null, 4 },
                    { 741, 1, "1001", 1, "现金", 1, null, 3 },
                    { 742, 1, "1002", 1, "银行存款", 2, null, 3 },
                    { 743, 1, "1009", 1, "其他货币资金", 3, null, 3 },
                    { 744, 1, "1101", 1, "短期投资", 4, null, 3 },
                    { 745, 1, "1102", 2, "短期投资跌价准备", 5, null, 3 },
                    { 746, 1, "1111", 1, "应收票据", 6, null, 3 },
                    { 747, 1, "1121", 1, "应收账款", 7, null, 3 },
                    { 748, 1, "1122", 1, "其他应收款", 8, null, 3 },
                    { 749, 1, "1131", 2, "坏账准备", 9, null, 3 },
                    { 750, 1, "1141", 1, "预付账款", 10, null, 3 },
                    { 751, 1, "1201", 1, "存货", 11, null, 3 },
                    { 752, 1, "1202", 2, "存货跌价准备", 12, null, 3 },
                    { 753, 1, "1301", 1, "待摊费用", 13, null, 3 },
                    { 754, 1, "1401", 1, "长期股权投资", 14, null, 3 },
                    { 755, 1, "1402", 1, "长期债权投资", 15, null, 3 },
                    { 756, 1, "1421", 2, "长期投资减值准备", 16, null, 3 },
                    { 757, 1, "1501", 1, "固定资产", 17, null, 3 },
                    { 758, 1, "1502", 2, "累计折旧", 18, null, 3 },
                    { 759, 1, "1505", 1, "在建工程", 19, null, 3 },
                    { 760, 1, "1506", 1, "文物文化资产", 20, null, 3 },
                    { 761, 1, "1509", 1, "固定资产清理", 21, null, 3 },
                    { 762, 1, "1601", 1, "无形资产", 22, null, 3 },
                    { 763, 1, "1701", 1, "受托代理资产", 23, null, 3 },
                    { 764, 2, "2101", 2, "短期借款", 24, null, 3 },
                    { 765, 2, "2201", 2, "应付票据", 25, null, 3 },
                    { 766, 2, "2202", 2, "应付账款", 26, null, 3 },
                    { 767, 2, "2203", 2, "预收账款", 27, null, 3 },
                    { 768, 2, "2204", 2, "应付工资", 28, null, 3 },
                    { 769, 2, "2206", 2, "应交税金", 29, null, 3 },
                    { 770, 2, "220601", 2, "营业税", 30, "2206", 3 },
                    { 771, 2, "220602", 2, "增值税", 31, "2206", 3 },
                    { 772, 2, "220603", 2, "所得税", 32, "2206", 3 },
                    { 773, 2, "220604", 2, "房产税", 33, "2206", 3 },
                    { 774, 2, "220605", 2, "个人所得税", 34, "2206", 3 },
                    { 775, 2, "2209", 2, "其他应付款", 35, null, 3 },
                    { 776, 2, "2301", 2, "预提费用", 36, null, 3 },
                    { 777, 2, "2401", 2, "预计负债", 37, null, 3 },
                    { 778, 2, "2501", 2, "长期借款", 38, null, 3 },
                    { 779, 2, "2502", 2, "长期应付款", 39, null, 3 },
                    { 780, 2, "2601", 2, "受托代理负债", 40, null, 3 },
                    { 781, 3, "3101", 2, "非限定性净资产", 41, null, 3 },
                    { 782, 3, "3102", 2, "限定性净资产", 42, null, 3 },
                    { 783, 5, "4101", 2, "捐赠收入", 43, null, 3 },
                    { 784, 5, "4201", 2, "会费收入", 44, null, 3 },
                    { 785, 5, "420101", 2, "团体会费", 45, "4201", 3 },
                    { 786, 5, "420102", 2, "个人会费", 46, "4201", 3 },
                    { 787, 5, "4301", 2, "提供服务收入", 47, null, 3 },
                    { 788, 5, "430101", 2, "学杂费收入", 48, "4301", 3 },
                    { 789, 5, "430102", 2, "医疗收入", 49, "4301", 3 },
                    { 790, 5, "430103", 2, "培训收入", 50, "4301", 3 },
                    { 791, 5, "4401", 2, "政府补助收入", 51, null, 3 },
                    { 792, 5, "4501", 2, "商品销售收入", 52, null, 3 },
                    { 793, 5, "4601", 2, "投资收益", 53, null, 3 },
                    { 794, 5, "4901", 2, "其他收入", 54, null, 3 },
                    { 795, 5, "5101", 1, "业务活动成本", 55, null, 3 },
                    { 796, 5, "5201", 1, "管理费用", 56, null, 3 },
                    { 797, 5, "520101", 1, "工资奖金", 57, "5201", 3 },
                    { 798, 5, "520102", 1, "福利费", 58, "5201", 3 },
                    { 799, 5, "520103", 1, "社会保障费", 59, "5201", 3 },
                    { 800, 5, "520104", 1, "办公费", 60, "5201", 3 },
                    { 801, 5, "520105", 1, "水电费", 61, "5201", 3 },
                    { 802, 5, "520106", 1, "差旅费", 62, "5201", 3 },
                    { 803, 5, "520107", 1, "折旧费", 63, "5201", 3 },
                    { 804, 5, "520108", 1, "离退休人员工资补助", 64, "5201", 3 },
                    { 805, 5, "520109", 1, "中介费", 65, "5201", 3 },
                    { 806, 5, "520110", 1, "应偿还受赠资产", 66, "5201", 3 },
                    { 807, 5, "520111", 1, "业务招待费", 67, "5201", 3 },
                    { 808, 5, "520112", 1, "广告费和业务宣传费", 68, "5201", 3 },
                    { 809, 5, "520113", 1, "通讯费", 69, "5201", 3 },
                    { 810, 5, "520114", 1, "修理费", 70, "5201", 3 },
                    { 811, 5, "520115", 1, "租赁费", 71, "5201", 3 },
                    { 812, 5, "520116", 1, "公积金", 72, "5201", 3 },
                    { 813, 5, "520117", 1, "汽车费用", 73, "5201", 3 },
                    { 814, 5, "520118", 1, "待摊费用摊销", 74, "5201", 3 },
                    { 815, 5, "520119", 1, "工会经费", 75, "5201", 3 },
                    { 816, 5, "520120", 1, "研究费用", 76, "5201", 3 },
                    { 817, 5, "520199", 1, "其他", 77, "5201", 3 },
                    { 818, 5, "5301", 1, "筹资费用", 78, null, 3 },
                    { 819, 5, "530101", 1, "借款费用", 79, "5301", 3 },
                    { 820, 5, "530102", 1, "汇兑损失", 80, "5301", 3 },
                    { 821, 5, "530103", 1, "活动费", 81, "5301", 3 },
                    { 822, 5, "530104", 1, "宣传资料费", 82, "5301", 3 },
                    { 823, 5, "530105", 1, "其他", 83, "5301", 3 },
                    { 824, 5, "5401", 1, "其他费用", 84, null, 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 468);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 469);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 470);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 471);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 472);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 473);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 474);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 475);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 476);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 477);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 478);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 479);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 480);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 481);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 482);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 483);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 484);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 485);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 486);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 487);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 488);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 489);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 490);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 491);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 492);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 493);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 494);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 495);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 496);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 497);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 498);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 499);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 500);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 501);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 502);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 503);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 504);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 505);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 506);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 507);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 508);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 509);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 510);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 511);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 512);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 513);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 514);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 515);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 516);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 517);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 518);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 519);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 520);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 521);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 522);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 523);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 524);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 525);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 526);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 527);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 528);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 529);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 530);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 531);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 532);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 533);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 534);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 535);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 536);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 537);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 538);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 539);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 540);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 541);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 542);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 543);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 544);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 545);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 546);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 547);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 548);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 549);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 550);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 551);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 552);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 553);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 554);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 555);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 556);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 557);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 558);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 559);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 560);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 561);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 562);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 563);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 564);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 565);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 566);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 567);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 568);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 569);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 570);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 571);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 572);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 573);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 574);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 575);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 576);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 577);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 578);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 579);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 580);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 581);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 582);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 583);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 584);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 585);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 586);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 587);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 588);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 589);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 590);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 591);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 592);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 593);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 594);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 595);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 596);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 597);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 598);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 599);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 600);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 601);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 602);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 603);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 604);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 605);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 606);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 607);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 608);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 609);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 610);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 611);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 612);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 613);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 614);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 615);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 616);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 617);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 618);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 619);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 620);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 621);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 622);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 623);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 624);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 625);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 626);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 627);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 628);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 629);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 630);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 631);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 632);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 633);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 634);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 635);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 636);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 637);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 638);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 639);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 640);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 641);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 642);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 643);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 644);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 645);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 646);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 647);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 648);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 649);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 650);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 651);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 652);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 653);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 654);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 655);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 656);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 657);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 658);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 659);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 660);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 661);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 662);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 663);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 664);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 665);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 666);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 667);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 668);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 669);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 670);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 671);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 672);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 673);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 674);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 675);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 676);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 677);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 678);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 679);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 680);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 681);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 682);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 683);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 684);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 685);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 686);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 687);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 688);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 689);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 690);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 691);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 692);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 693);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 694);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 695);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 696);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 697);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 698);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 699);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 700);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 701);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 702);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 703);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 704);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 705);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 706);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 707);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 708);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 709);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 710);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 711);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 712);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 713);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 714);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 715);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 716);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 717);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 718);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 719);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 720);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 721);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 722);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 723);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 724);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 725);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 726);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 727);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 728);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 729);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 730);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 731);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 732);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 733);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 734);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 735);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 736);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 737);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 738);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 739);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 740);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 741);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 742);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 743);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 744);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 745);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 746);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 747);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 748);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 749);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 750);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 751);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 752);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 753);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 754);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 755);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 756);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 757);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 758);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 759);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 760);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 761);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 762);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 763);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 764);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 765);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 766);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 767);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 768);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 769);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 770);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 771);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 772);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 773);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 774);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 775);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 776);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 777);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 778);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 779);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 780);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 781);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 782);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 783);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 784);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 785);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 786);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 787);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 788);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 789);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 790);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 791);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 792);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 793);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 794);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 795);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 796);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 797);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 798);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 799);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 800);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 801);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 802);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 803);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 804);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 805);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 806);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 807);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 808);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 809);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 810);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 811);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 812);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 813);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 814);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 815);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 816);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 817);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 818);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 819);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 820);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 821);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 822);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 823);

            migrationBuilder.DeleteData(
                table: "preset_accounts",
                keyColumn: "id",
                keyValue: 824);
        }
    }
}
