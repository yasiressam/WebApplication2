using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class SiteSettings
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "الرجاء إدخال بريد إلكتروني صحيح")]
        public string ContactEmail { get; set; } = "info@iraqinews.com";

        [Display(Name = "رقم الهاتف")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string ContactPhone { get; set; } = "+964 770 000 0000";

        [Display(Name = "العنوان")]
        public string? SiteAddress { get; set; } = "العراق - بغداد";

        [Display(Name = "وصف الموقع")]
        [Required(ErrorMessage = "وصف الموقع مطلوب")]
        public string SiteDescription { get; set; } = "منصة إلكترونية متكاملة تهدف إلى توفير خدمات إلكترونية للمواطنين العراقيين بأسلوب عصري وسهل.";

        // وسائل التواصل الاجتماعي (الثلاثة فقط)
        [Display(Name = "رابط فيسبوك")]
        [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
        public string? FacebookUrl { get; set; } = "";

        [Display(Name = "رابط إنستغرام")]
        [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
        public string? InstagramUrl { get; set; } = "";


        [Display(Name = "عنوان إشعار قبول الترقية")]
        public string PromotionApprovedTitle { get; set; } = "🎉 تهانينا! تمت الموافقة على طلب الترقية";

        [Display(Name = "نص إشعار قبول الترقية")]
        public string PromotionApprovedMessage { get; set; } = "تمت ترقية حسابك إلى 'فرد' بنجاح.";

        [Display(Name = "عنوان إشعار رفض الترقية")]
        public string PromotionRejectedTitle { get; set; } = "❌ عذراً، لم يتم الموافقة على طلبك";

        [Display(Name = "نص إشعار رفض الترقية")]
        public string PromotionRejectedMessage { get; set; } = "سبب الرفض: {reason}";

        [Display(Name = "عنوان إشعار قبول البيانات الأساسية")]
        public string BasicInfoApprovedTitle { get; set; } = "✅ تمت الموافقة على بياناتك الأساسية";

        [Display(Name = "نص إشعار قبول البيانات الأساسية")]
        public string BasicInfoApprovedMessage { get; set; } = "يمكنك الآن إكمال البيانات الإضافية";

        [Display(Name = "عنوان إشعار رفض البيانات الأساسية")]
        public string BasicInfoRejectedTitle { get; set; } = "❌ لم يتم الموافقة على بياناتك الأساسية";

        [Display(Name = "نص إشعار رفض البيانات الأساسية")]
        public string BasicInfoRejectedMessage { get; set; } = "سبب الرفض: {reason}";

        [Display(Name = "عنوان إشعار التعيين الإداري المباشر")]
        public string DirectAssignmentTitle { get; set; } = "✅ تم التعيين الإداري";

        [Display(Name = "نص إشعار التعيين الإداري المباشر")]
        public string DirectAssignmentMessage { get; set; } = "تم تعيينك مباشرة كـ {levelName}{managedNamePart}{governoratePart}";

        [Display(Name = "عنوان إشعار إرسال استمارة التكليف")]
        public string AssignmentFormTitle { get; set; } = "📋 استمارة تكليف إداري";

        [Display(Name = "نص إشعار إرسال استمارة التكليف")]
        public string AssignmentFormMessage { get; set; } = "تم إرسال استمارة إليك لإكمال طلب التكليف كـ {roleName} {levelType}. يرجى تعبئة البيانات المطلوبة ثم الإرسال.";

        [Display(Name = "عنوان إشعار استمارة بانتظار المراجعة")]
        public string AssignmentSubmittedTitle { get; set; } = "📨 استمارة تكليف بانتظار المراجعة";

        [Display(Name = "نص إشعار استمارة بانتظار المراجعة")]
        public string AssignmentSubmittedMessage { get; set; } = "قام المستخدم {fullName} بإرسال استمارة {levelName}{managedNamePart} بانتظار موافقتك";

        [Display(Name = "عنوان إشعار قبول التكليف الإداري")]
        public string AssignmentApprovedTitle { get; set; } = "✅ تمت الموافقة على التكليف الإداري";

        [Display(Name = "نص إشعار قبول التكليف الإداري")]
        public string AssignmentApprovedMessage { get; set; } = "تمت الموافقة على طلب تكليفك كـ {levelName}{managedNamePart}";

        [Display(Name = "عنوان إشعار رفض التكليف الإداري")]
        public string AssignmentRejectedTitle { get; set; } = "❌ تم رفض طلب التكليف الإداري";

        [Display(Name = "نص إشعار رفض التكليف الإداري")]
        public string AssignmentRejectedMessage { get; set; } = "تم رفض طلب التكليف الإداري الخاص بك.{reasonPart}";

        [Display(Name = "عنوان إشعار إلغاء التكليف الإداري")]
        public string AssignmentRemovedTitle { get; set; } = "تم إلغاء التكليف الإداري";

        [Display(Name = "نص إشعار إلغاء التكليف الإداري")]
        public string AssignmentRemovedMessage { get; set; } = "تم إلغاء تكليفك كـ {levelName}{managedNamePart}{governoratePart}";

        [Display(Name = "عنوان إشعار ترقية سوبر أدمن")]
        public string SuperAdminAssignedTitle { get; set; } = "👑 تمت ترقيتك إلى سوبر أدمن";

        [Display(Name = "نص إشعار ترقية سوبر أدمن")]
        public string SuperAdminAssignedMessage { get; set; } = "تمت ترقية حسابك إلى سوبر أدمن";

        [Display(Name = "عنوان إشعار ترقية أدمن")]
        public string AdminAssignedTitle { get; set; } = "🛡️ تمت ترقيتك إلى أدمن";

        [Display(Name = "نص إشعار ترقية أدمن")]
        public string AdminAssignedMessage { get; set; } = "تمت ترقية حسابك إلى أدمن";

        [Display(Name = "عنوان إشعار تعيين محرر أخبار")]
        public string NewsEditorAssignedTitle { get; set; } = "📝 تم تعيينك كمحرر أخبار";

        [Display(Name = "نص إشعار تعيين محرر أخبار")]
        public string NewsEditorAssignedMessage { get; set; } = "يمكنك الآن إدارة الأخبار";

        [Display(Name = "عنوان إشعار تعيين مشاهد خريطة")]
        public string MapViewerAssignedTitle { get; set; } = "🗺️ تم تعيينك كمشاهد خريطة";

        [Display(Name = "نص إشعار تعيين مشاهد خريطة")]
        public string MapViewerAssignedMessage { get; set; } = "يمكنك الآن مشاهدة الخريطة";

        [Display(Name = "عنوان إشعار ترقية إلى فرد من الأدوار")]
        public string MemberAssignedTitle { get; set; } = "⭐ تمت ترقيتك إلى فرد";

        [Display(Name = "نص إشعار ترقية إلى فرد من الأدوار")]
        public string MemberAssignedMessage { get; set; } = "تمت ترقية حسابك إلى فرد";

        [Display(Name = "عنوان إشعار تعديل البيانات الشخصية")]
        public string ProfileUpdatedTitle { get; set; } = "📝 تم تعديل بياناتك الشخصية";

        [Display(Name = "نص إشعار تعديل البيانات الشخصية")]
        public string ProfileUpdatedMessage { get; set; } = "للاطلاع قم بزيارة ملفك الشخصي";

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
