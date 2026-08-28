using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace TM_PE.Model
{
    // One login account per Employee. Username is fixed at creation time
    // (derived from the company name, the employee's ID number, and their
    // role) and is never editable afterward — see UsernamePolicy below.
    [Table("tbl_useraccount")]
    public class UserAccount
    {
        [Key]
        public int UserAccountID { get; set; }

        [Required]
        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        // PBKDF2 hash + its own random salt — never store the plaintext
        // password anywhere, including in memory longer than needed.
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string PasswordSalt { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime? LastLogin { get; set; }

        public bool IsActive { get; set; } = true;
    }

    // Central place for the username format and password rule, so both the
    // Create page and any future page compute/validate them the same way
    // instead of duplicating the logic.
    public static class UsernamePolicy
    {
        public const string CompanyName = "Pakonek";

        // 8-10 digit numeric password, matching the "8-10 digit" restriction.
        public const string PasswordPattern = @"^\d{8,10}$";

        public static string RoleInitial(RoleType role) => role switch
        {
            RoleType.FieldTechnician => "T",
            RoleType.OfficeStaff => "O",
            RoleType.Manager => "M",
            RoleType.Admin => "A",
            _ => "X"
        };

        // e.g. Pakonek-T001 for Field Technician EmployeeId 1.
        public static string BuildUsername(Employee employee) =>
            $"{CompanyName}-{RoleInitial(employee.RoleType)}{employee.EmployeeId:000}";
    }

    // PBKDF2-SHA256 password hashing using only the base class library — no
    // extra NuGet package needed.
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static (string Hash, string Salt) Hash(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        public static bool Verify(string password, string hash, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expected = Convert.FromBase64String(hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
    }
}