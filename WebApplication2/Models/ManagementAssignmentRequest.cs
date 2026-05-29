using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class ManagementAssignmentRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string RequestedByUserId { get; set; } = string.Empty;

        [Required]
        public string Governorate { get; set; } = string.Empty;

        public int? AffiliationEntityId { get; set; }
        public int? DivisionId { get; set; }
        public int? SectionId { get; set; }
        public int? GroupId { get; set; }

        [Required]
        public string ManagementLevel { get; set; } = string.Empty;
        // Entity / Division / Section / Group

        [Required]
        public string AssignmentRole { get; set; } = "Manager";
        // Manager / Assistant

        [Required]
        public string Status { get; set; } = "PendingUserResponse";
        // PendingUserResponse
        // SubmittedToSuperAdmin
        // Approved
        // RejectedByUser
        // RejectedBySuperAdmin

        public string? UserNotes { get; set; }
        public string? SuperAdminNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UserRespondedAt { get; set; }
        public DateTime? SuperAdminReviewedAt { get; set; }
    }
}