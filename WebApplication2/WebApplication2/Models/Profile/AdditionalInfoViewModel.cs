namespace WebApplication2.Models.Profile
{
    public class AdditionalInfoViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }

        public DocumentsViewModel Documents { get; set; } = new();
        public AffiliationViewModel Affiliation { get; set; } = new();
        public MembershipViewModel Memberships { get; set; } = new();

        public List<string> AffiliationEntities { get; set; } = new();
        public List<string> DivisionsList { get; set; } = new();
        public List<string> SectionsList { get; set; } = new();
        public List<string> GroupsList { get; set; } = new();
        public List<string> UnionsList { get; set; } = new();
        public List<string> FederationsList { get; set; } = new();
        public List<string> FederationDivisionsList { get; set; } = new();
        public List<string> FederationSectionsList { get; set; } = new();
        public List<string> FederationGroupsList { get; set; } = new();
        public List<string> AssociationsList { get; set; } = new();
        public List<string> NgosList { get; set; } = new();
    }
}