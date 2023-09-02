namespace AwsAuthSync
{
    internal class MapRole
    {
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
        public string Username { get; set; }

        public string Rolearn { get; set; }

        public List<string> Groups { get; set; }
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.

        public override string ToString()
        {
            string result = string.Empty;

            result += string.Format("rolearn: {0}\n", Rolearn);
            result += string.Format("  username: {0}\n", Username);
            string groups = string.Empty;
            foreach (var group in Groups)
            {
                groups += string.Format("    - {0}\n", group);
            }

            result += string.Format("  groups:\n{0}", groups);

            return result;
        }
    }

}