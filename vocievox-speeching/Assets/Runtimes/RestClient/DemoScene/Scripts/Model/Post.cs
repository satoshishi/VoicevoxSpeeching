namespace Models
{
    using System;

    [Serializable]
    public class Post
    {
        public int Id;

        public int UserId;

        public string Title;

        public string Body;

        public override string ToString()
        {
            return UnityEngine.JsonUtility.ToJson(this, true);
        }
    }
}

