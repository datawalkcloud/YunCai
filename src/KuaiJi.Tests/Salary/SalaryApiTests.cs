using System.Net;
using System.Net.Http.Json;
using KuaiJi.Tests.Support;
using Xunit;

namespace KuaiJi.Tests.Salary;

/// <summary>
/// 工资 HTTP 集成测试：走真实组合根（认证、模型绑定、控制器 try/catch 把 SalaryException
/// 转成 400 + 中文 message，未命中主键走 404），数据库换成内存 SQLite。
/// 锁住的是「HTTP 边界上的行为」，与 SalaryServiceTests（直接调用应用服务）互补：
/// - 未知账套 → 400「指定的账套不存在。」（EnsureAccountSetExistsAsync 的文案）；
/// - 员工编号判重 → 400 透出「已存在」中文文案；
/// - 更新不存在的员工 → 404（控制器把 null 结果转 NotFound，而非 400）。
/// </summary>
public sealed class SalaryApiTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();
    private readonly HttpClient _client;

    public SalaryApiTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose() => _factory.Dispose();

    /// <summary>经 POST /AccountSets 建一个独立账套，返回其 Id。</summary>
    private async Task<Guid> NewAccountSetAsync(string name)
    {
        var created = await _client.PostAsJsonAsync("/AccountSets", new
        {
            name,
            companyName = $"{name}有限公司",
            standard = 1,
            taxpayerType = 1,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
        });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<AccountSetModel>())!.Id;
    }

    private static string EmployeesPath(Guid accountSetId) => $"/accountsets/{accountSetId}/salary/employees";

    private static object EmployeeBody(string code, string name, string? dept = null) => new
    {
        code,
        name,
        gender = 1,
        idType = 1,
        dept,
        status = 1,
    };

    [Fact]
    public async Task GetEmployees_UnknownAccountSet_Returns400WithChineseMessage()
    {
        var response = await _client.GetAsync(EmployeesPath(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("指定的账套不存在", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateEmployee_DuplicateCode_Returns400WithChineseMessage()
    {
        var id = await NewAccountSetAsync("工资判重");

        var first = await _client.PostAsJsonAsync(EmployeesPath(id), EmployeeBody("E001", "张三", "销售部"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync(EmployeesPath(id), EmployeeBody("E001", "李四"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("员工编号「E001」已存在", body);
    }

    [Fact]
    public async Task CreateEmployee_Valid_ReturnsDto_AndListReflectsIt()
    {
        // 新建成功返回 200 + 员工 DTO（含中文性别/状态文本），列表能查到这一条。
        var id = await NewAccountSetAsync("工资建员");

        var created = await _client.PostAsJsonAsync(EmployeesPath(id), EmployeeBody("E001", "张三", "销售部"));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var employee = await created.Content.ReadFromJsonAsync<EmployeeModel>();
        Assert.Equal("E001", employee!.Code);
        Assert.Equal("销售部", employee.Dept);
        Assert.Equal("男", employee.GenderText);
        Assert.Equal("在职", employee.StatusText);

        var list = await _client.GetFromJsonAsync<List<EmployeeModel>>(EmployeesPath(id));
        Assert.Equal("E001", Assert.Single(list!).Code);
    }

    [Fact]
    public async Task UpdateEmployee_UnknownId_Returns404()
    {
        var id = await NewAccountSetAsync("工资改员");

        var response = await _client.PutAsJsonAsync(
            $"{EmployeesPath(id)}/{Guid.NewGuid()}", EmployeeBody("E002", "王五"));

        // 控制器把「员工不存在」的 null 结果转成 404，而不是判重/校验的 400。
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record AccountSetModel(Guid Id);

    private sealed record EmployeeModel(
        Guid Id,
        string Code,
        string Name,
        string GenderText,
        string Dept,
        string StatusText);
}
