using Cysharp.Threading.Tasks;

public interface IPlatformLogin
{

    public abstract UniTask<string> GetPlatformToken();
}
