namespace TaxOfficeClient.Model.Manager;

public class ApiResult
{
	public bool Success { get; set; }

	public string Message { get; set; }

	public ApiParameters Data { get; set; }
}
