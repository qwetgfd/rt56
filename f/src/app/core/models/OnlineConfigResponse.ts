import { FileColumnMapping } from "./additionalSettings";
import {  RequestDatabaseSetting } from "./databaseSettings";
import { ExcelRule, flpConfigurationRuleSet } from "./DataInsider";
import { FileConfiguration, ProcessSettings } from "./fileConfiguration";

export interface OnlineConfigResponse {
  processSettings: RequestProcessSettings;
  fileSettings: FileSettingList[];
}


export interface FileSettingList { 
    tabName:string;  
    ignoreSheet: boolean;
    additionalSettings : FileConfiguration | null;
    fileColumnMapping : FileColumnMapping[]; 
    databaseSettings : RequestDatabaseSetting | null;
    ruleSets : ExcelRule[] | null;  //flpConfigurationRuleSet[] | null;
}


export interface RequestProcessSettings extends ProcessSettings{
  process_name: string;
  description: string;
  regionId: string;
  subRegionId: string;
  clientId: string;
  campaignId : string;
  internalCampaignId : string;
}
