// ملف: Models/Profile/DocumentsViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models.Profile
{
    public class DocumentsViewModel
    {
        // ❌ تم إزالة IdentityCardN و IdentityDate لأنهما خاصان بالمعلومات الأساسية
        // ❌ تم إزالة RationN و RationCenter (البطاقة التموينية ومركز التموين)

        [Display(Name = "رقم بطاقة الناخب")]
        public string? VoterCardNumber { get; set; }

        [Display(Name = "رقم مركز الاقتراع")]
        public string? PollingCenterNumber { get; set; }
    }
}