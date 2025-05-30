namespace function_prohibitedregistration.Model
{
    public class Member
    {
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? UnformattedUsername { get; set; }
        public int LevelId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int CompanyId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public string? Currency { get; set; }
        public string? StatusType { get; set; }
        public string? UniqueId { get; set; }
        public string? SecondaryUniqueId { get; set; }
        public string? JurisdictionCode { get; set; }
        public string? CountryCode { get; set; }
        public string? PromoCode { get; set; }
        public string? AffCode { get; set; }
        public string? Btag { get; set; }
        public string? CSource { get; set; }
        public string? CMedium { get; set; }
        public string? CName { get; set; }
        public string? RefURL { get; set; }
        public string? Host { get; set; }
    }
}
