using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ArcadiaCSNavAPI.Models
{
    [Table("tracks")]
    public class Track : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("track_name")]
        public string TrackName { get; set; }

        [Column("completed_id")]
        public int CompletedId { get; set; } // the node id that was checked for completion

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}