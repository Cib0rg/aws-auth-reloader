using Amazon.IdentityManagement.Model;
using System.Text;

namespace AwsAuthSync
{
    internal class AWSUser
    {
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
        public string Arn { get; set; }

        public string Name { get; set; }

        private List<string> groups = new();

        // Little crutch to save groups info from aws-auth (and back)
        public List<string> Groups
        {
            get
            {
                groups.Sort();
                return groups;
            }
            set
            {
                groups = value;
            }
        }

        public List<Tag> Tags { get; set; }
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.

        /// <summary>
        /// Overrided method to beatify output
        /// </summary>
        /// <returns>Text representation of tags on aws account</returns>
        public override string ToString()
        {
            var tags = new StringBuilder();

            foreach (var tag in Tags)
            {
                tags.Append(String.Format("Key: {0}, Value: {1} |", tag.Key, tag.Value));
            }

            String result = String.Format("Username: {0}, Tags: {1}", Name, tags.ToString());

            return result;
        }
    }
}
