// Models/Request/RequestRecipient.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Request
{
    public class RequestRecipient
    {
        [Key]
        public int Id { get; set; }

        public int RequestId { get; set; }

        public string RecipientId { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public bool HasResponded { get; set; } = false;

        public DateTime? RespondedAt { get; set; }
    }
}