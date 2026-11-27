namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C#의 record 문법을 Unity에서 쓰려면 꼭 있어야 하는 빈 클래스예요.
    /// (Unity가 쓰는 .NET 버전에는 이 클래스가 빠져 있어서 직접 만들어 줘요.)
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
