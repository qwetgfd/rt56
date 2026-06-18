using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration
{
    public class EnableDisableProcessByFlpConfigurationIdResponseDto : DatabaseResponse
    {
    }

    public class EnableDisableProcessByFlpConfigurationIdRequestDto 
    {
        public string flpConfigurationIds { get; set; }
        public string userName { get; set; }
        public string created_by { get; set; }
        public bool activeStatus { get; set; }        
    }

    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string userName { get; set; }
        public string TokenHash { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public bool Revoked { get; set; }
        public string? RevocationReason { get; set; }
        public Guid? ReplacedById { get; set; }
        public int Version { get; set; }
    }


    public class RevokedToken
    {
        public string Jti { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime RevokedAt { get; set; }
    }

}
