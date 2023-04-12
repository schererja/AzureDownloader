namespace AzureDownloader
{

    public class Artifact
    {
        public int count { get; set; }

        public ArtifactValue[] value { get; set; }
    }

    public class ArtifactValue
    {
        public int id { get; set; }
        public string name { get; set; }
        public string source { get; set; }
        public Resource resource { get; set; }
    }

    public class Resource
    {
        public string type { get; set; }
        public string data { get; set; }
        public ArtifactProperties properties { get; set; }
        public string url { get; set; }
        public string downloadUrl { get; set; }
    }

    public class ArtifactProperties
    {
        public string localpath { get; set; }
        public string artifactsize { get; set; }
    }


}
