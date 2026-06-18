using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Account
{
    public class ApplicationRegistrationRequestDto
    {
        [Required]
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid applicationName.")]
        [SafeString]
        public string ApplicationName { get; set; }

        [Required]
        [MaxLength(800, ErrorMessage = "The {0} field cannot exceed {1} characters.")]
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid applicationDescription.")]
        [SafeString]
        public string ApplicationDescription { get; set; }

        [Required]
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid applicationOwnerName.")]
        [SafeString]
        public string ApplicationOwnerName { get; set; }

        [Required]
        [EmailAddress]
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid applicationOwnerEmail.")]
        [SafeString]
        public string ApplicationOwnerEmail { get; set; }
    }
}
