using System.Diagnostics;

namespace Timelapse.Editor.Util
{
    public class MyTrace
    {
        public static void MethodName(string message, [CallerMemberName] string memberName = "")
        {
            // Uncomment to enable tracing
            // if (message.Equals (System.String.Empty))
            //    Debug.Print("**: "  + memberName);
            // else
            //    Debug.Print("**" + message + ": " + memberName);
        }
    }
}
