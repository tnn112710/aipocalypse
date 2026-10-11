using System;
using System.Linq;
using System.Reflection;
using System.Text;

public static class BdApi
{
    public static string Dump(string typeNames)
    {
        var sb = new StringBuilder();
        var all = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("Opsive"))
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); } })
            .ToArray();
        foreach (var name in typeNames.Split(','))
        {
            var t = all.FirstOrDefault(x => x.FullName == name.Trim().Replace("/", "+"));
            if (t == null) { sb.AppendLine("MISSING " + name); continue; }
            sb.AppendLine("===== " + t.FullName + " : " + t.BaseType + " [" + t.Assembly.GetName().Name + "]");
            if (t.IsEnum) { sb.AppendLine("  enum: " + string.Join(", ", Enum.GetNames(t))); continue; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var m in t.GetMembers(flags).OrderBy(m => m.MemberType.ToString()).ThenBy(m => m.Name))
            {
                bool pub = m is MethodInfo mi ? (mi.IsPublic || mi.IsFamily) && !mi.IsSpecialName
                         : m is PropertyInfo pi ? (pi.GetMethod?.IsPublic ?? false) || (pi.GetMethod?.IsFamily ?? false)
                         : m is FieldInfo fi ? fi.IsPublic || fi.IsFamily
                         : m is EventInfo ? true : false;
                if (!pub) continue;
                sb.AppendLine("  " + m.MemberType + " " + m);
            }
        }
        return sb.ToString();
    }

    public static string Find(string pattern)
    {
        return string.Join("\n", AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("Opsive"))
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); } })
            .Where(t => t.IsVisible && t.FullName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(t => t.FullName + "  [" + t.Assembly.GetName().Name + "]").OrderBy(s => s));
    }
}
