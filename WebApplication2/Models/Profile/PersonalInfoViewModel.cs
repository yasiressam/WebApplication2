// ملف: Models/Profile/PersonalInfoViewModel.cs
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebApplication2.Models.Profile
{
    public class PersonalInfoViewModel
    {
        private string? _fullName;
        private string? _motherName;

        [Display(Name = "الاسم الرباعي")]
        [Required(ErrorMessage = "الاسم الرباعي مطلوب")]
        [IraqiName(4, ErrorMessage = "يرجى إدخال الاسم الرباعي كاملاً وبحروف عربية أو كوردية")]
        public string? FullName
        {
            get => _fullName;
            set => _fullName = IraqiNameText.Normalize(value);
        }

        [Display(Name = "اللقب")]
        [Required(ErrorMessage = "اللقب مطلوب")]
        public string? LastName { get; set; }

        [Display(Name = "اسم الأم")]
        [Required(ErrorMessage = "اسم الأم مطلوب")]
        [IraqiName(3, ErrorMessage = "يرجى إدخال اسم الأم ثلاثياً وبحروف عربية أو كوردية")]
        public string? MotherName
        {
            get => _motherName;
            set => _motherName = IraqiNameText.Normalize(value);
        }

        [Display(Name = "تاريخ الميلاد")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Required(ErrorMessage = "تاريخ الميلاد مطلوب")]
        public DateTime DateOfBirth { get; set; } = DateTime.Now.AddYears(-18);

        [Display(Name = "الجنس")]
        [Required(ErrorMessage = "الجنس مطلوب")]
        public string? Gender { get; set; }

        // ✅ الحالة الاجتماعية
        [Display(Name = "الحالة الاجتماعية")]
        [Required(ErrorMessage = "الحالة الاجتماعية مطلوبة")]
        public string? MaritalStatus { get; set; }

        [Display(Name = "التحصيل الدراسي")]
        [Required(ErrorMessage = "التحصيل الدراسي مطلوب")]
        public string? Education { get; set; }

        [Display(Name = "الاختصاص")]
        [Required(ErrorMessage = "الاختصاص مطلوب")]
        public string? Specialization { get; set; }

        [Display(Name = "رقم الهاتف")]
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^07\d{9}$", ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 07 ويتكون من 11 رقم")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "الصورة الشخصية")]
        public string? CoverImage { get; set; }

        // ========== حقول الطالب الجامعي ==========
        [Display(Name = "نوع الجامعة")]
        public string? UniversityType { get; set; } // "اهلي" أو "حكومي"

        [Display(Name = "نوع المؤسسة")]
        public string? InstitutionType { get; set; } // "جامعة" أو "معهد"

        [Display(Name = "اسم الجامعة/المعهد")]
        public string? InstitutionName { get; set; }

        [Display(Name = "الكلية/القسم")]
        public string? FacultyDepartment { get; set; }

        [Display(Name = "نوع الدراسة")]
        public string? StudyType { get; set; } // "صباحي" أو "مسائي"

        [Display(Name = "المرحلة")]
        public string? StudyStage { get; set; } // "المرحلة الأولى", "الثانية", إلخ

        // ========== قائمة المراحل الدراسية (للقائمة المنسدلة) ==========
        public List<string> StudyStagesList { get; set; } = new();
    }

    public sealed class IraqiNameAttribute : ValidationAttribute
    {
        private readonly int _minimumWords;

        public IraqiNameAttribute(int minimumWords)
        {
            _minimumWords = minimumWords;
        }

        public override bool IsValid(object? value)
        {
            var text = IraqiNameText.Normalize(value?.ToString());
            if (string.IsNullOrWhiteSpace(text))
                return true;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < _minimumWords)
                return false;

            return text.All(IsAllowedNameCharacter);
        }

        private static bool IsAllowedNameCharacter(char character)
        {
            if (char.IsWhiteSpace(character))
                return true;

            var category = char.GetUnicodeCategory(character);
            return category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark;
        }
    }

    public static class IraqiNameText
    {
        private static readonly Regex InvisibleCharacters = new("[\u200B-\u200D\uFEFF]", RegexOptions.Compiled);
        private static readonly Regex RepeatedWhitespace = new(@"\s+", RegexOptions.Compiled);

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var normalized = value.Normalize(NormalizationForm.FormKC);
            normalized = InvisibleCharacters.Replace(normalized, string.Empty);
            normalized = RepeatedWhitespace.Replace(normalized, " ").Trim();

            return normalized;
        }
    }
}
