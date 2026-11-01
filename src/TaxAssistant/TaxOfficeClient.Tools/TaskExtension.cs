using System.Threading.Tasks;

namespace TaxOfficeClient.Tools;

public static class TaskExtension
{
	public static void Forget(this Task task)
	{
		if (!task.IsCompleted || task.IsFaulted)
		{
			ForgetAwaited(task);
		}
		static async Task ForgetAwaited(Task task2)
		{
			try
			{
				await task2.ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
			}
		}
	}
}
