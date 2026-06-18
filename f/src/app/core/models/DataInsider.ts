import { ProcessConfigurationRequest } from "./processConfigurationlist";

export interface RuleTypes {
    ruleTypeId: number;
    ruleTypeName: string;
    category: string;
    description: string;
}
export interface SubRule {
    id: number;
    ruleTypeId: number;
    subRuleName: string;
    subRuleId: number;
}

export interface Patterns extends SingularPluras {
    patternId: number;
    ruleTypeId: number;
    patternName: string;
    subRuleId: number;
}

export interface LogicalOperator {
    id: number;
    operatorName: string;
}
export interface ConditionalOperator {
    conditionalOperatorId: number;
    conditionalOperatorName: string;
}

export interface RuleType {
    rule_name: string;
    columns: string[];
    //is_combination_rule: boolean;
    rule_description: string;
}

interface SingularPluras {
    sing: string;
    plu: string;
}

export interface RuleSetNames {
    ruleSetNameId: string;
    ruleSetName: string;
    creationDateTime: string;
    updationDateTime: string;
}

export interface ExcelRule {
    id: number;
    ruleSetNameId: string;
    ruleSetName: string;
    ruleTypeId: number;
    subRuleId: number;
    ruleColumnName: string[];
    ruleColumnName2: string; //test this is always string not []
    ruleDescription: string;
    description: string;
    isCombinationRule: boolean;
    prompt: string;
    format: string;
    patternId: number;
    conditionId: number;
    fromValue: number;
    toValue: number;
    isActive: boolean;
    isIrrelevant: boolean; //used to identify if rule is relevant to the set of columns in columnDataType
    isGlobal: boolean;
    ruleSetType: number; //1 ui rule process,  2 generic rule,
    isAllowNullOrEmptySpaces: boolean;
    spNameId: number;
    isUpdated : boolean;
    // parquetRuleColumnName:string[],
    // parquetRuleColumnName2 : string,
    // parquetruleDescription : string;
}

export interface flpConfigurationRuleSet {
    id: number;
    ruleTypeId: number;
    subRuleId: number;
    ruleColumnName: string;
    ruleDescription: string;
    isCombinationRule: boolean;
    prompt: string;
    format: string;
    patternId: number;
    conditionId: number;
    fromValue: number;
    toValue: number;
}

export interface RuleSetListRequest extends ProcessConfigurationRequest {

}

export interface RuleSetListResponse {
    response: RuleSetConfigurationList[],
    totalCount: number
}

export interface RuleSetConfigurationList {
    ruleSetNameId: string;
    ruleSetName: string;
    description: string;
    ruleCount: number;
    username: string;
    creationDateTime: string;
    isGlobal: boolean;

}

export interface PayLoad {
    ruleSets: Omit<ExcelRule, 'isIrrelevant'>[];
    created_by: string;
    username: string;
    description: string;
    ruleSetName : string;
}

export interface SPNames {
    spNameId: number;
    spName: string;
}

export interface DataAssistsRequest {
    //chatId: number;
    //userInput: string;
    //userContext: string;
    //userUpn: string;
    //chatArea: string;
    validationRules: string;
    //public IFormFile File
    //{
    //    get; set;
    //}
    fileHeaders: {ColumnHeaders : { Column: string, DataType: string }[]} ;
    //userSelectedDatabase: string;
    //responsetype: string;
    flowid: number;
    projectId: number;
    versionId: number;
    isDataValidation: boolean;
    isCodeSnippet: boolean;
    overrideExistingRules: boolean;
}