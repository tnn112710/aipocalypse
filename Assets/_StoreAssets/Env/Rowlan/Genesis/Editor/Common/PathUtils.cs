namespace Rowlan.Genesis
{
    public class PathUtils
    {
        public static string Normalize( string path)
        {
            return path.Replace("\\", "/");
        }
    }

}