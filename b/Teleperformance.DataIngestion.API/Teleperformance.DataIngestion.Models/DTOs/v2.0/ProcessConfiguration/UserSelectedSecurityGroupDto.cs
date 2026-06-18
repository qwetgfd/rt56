using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration
{
    public class UserSelectedSecurityGroupDto
    {
        [Required]
        public string SecurityGroupId { get; set; }
        [Required]
        public string LoginId { get; set; }
        public string UserName { get; set; }
    }
}
