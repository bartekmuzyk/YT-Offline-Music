using System.Threading.Tasks;

namespace YT_Offline_Music.Extensions;

public static class TaskExt
{
    public static T RunSynchronouslyWithResult<T>(this Task<T> task)
    {
        task.Wait();
        return task.Result;
    }
}