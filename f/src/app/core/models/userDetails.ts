export interface UserDetails {
  token: string;

  userFullName: string;
  FirstName: string;
  LastName: string;
  displayName: string;
  UPN: string;
  email: string;
  Image: string;
  employeeId: string;
  isAdmin: boolean;
}

export interface AzureUserGroup {
  value: AzureUserGroupId[];
}

export interface AzureUserGroupId {
  id: string;
  groupTypes: [];
  displayName: string;
  securityEnabled: boolean | null;
}
export interface SecurityGroup {
  securityGroupId: string;
  securityGroupName: string;
  userSelectedGroup: boolean;
}

export interface CampaignUserAccess {
  internalCampaignId : string;
  campaignId: string;
  campaignName: string;
  regionId: number;
  subRegionId: string;
  clientId: number;
}

export interface CampaignNames {
  campaignId: string;
  campaignName: string;
}
