import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { ConditionalOperator, DataAssistsRequest, ExcelRule, Patterns, RuleSetListRequest, RuleSetListResponse, RuleSetNames, RuleType, RuleTypes, SPNames, SubRule } from '../models/DataInsider';
import { APIResponse } from '../models/apiResponse';
import { Observable } from 'rxjs';
import { Prefixes } from '../models/LandingLayer/landingLayer';
@Injectable({
  providedIn: 'root',
})
export class DataInsiderService {
  private baseUrl = environment.apiEndpoint;
  rules: RuleTypes[] = [];
  subRules: SubRule[] = [];
  patterns: Patterns[] = [];
  constructor(private http: HttpClient) { }

  setHttpsRequestHeaders(apiVersion: string): HttpHeaders {
    let objHttpHeaders = new HttpHeaders();
    objHttpHeaders = objHttpHeaders.append(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    objHttpHeaders = objHttpHeaders.set(
      'Content-Type',
      'application/json; charset=utf-8'
    );
    objHttpHeaders = objHttpHeaders.set('Accept', 'application/json');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', apiVersion);
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }

  getRuleTypes() {
    this.rules = [];
    this.subRules = [];
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + 'api/ProcessConfiguration/GetDIRuleTypes';
    return this.http.get<APIResponse<RuleTypes[]>>(url, {
      headers: headers,
    });

  }

  getSPNames() {

    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + 'api/ProcessConfiguration/GetValidationSPNames';
    return this.http.get<APIResponse<SPNames[]>>(url, {
      headers: headers,
    });


  }
  getSubRule(ruleTypeId: number) {
    this.subRules = [];
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDISubRules?ruleTypeId=${ruleTypeId}`;
    return this.http.get<APIResponse<SubRule[]>>(url, {
      headers: headers,
    });
    // this.subRules.push(
    //   //      { id: 1, ruleId: 3, subRuleName: 'DataType' },
    //   { id: 1, ruleId: 3, subRuleName: 'Pattern', subRuleId: 0 },
    //   { id: 2, ruleId: 3, subRuleName: 'Length', subRuleId: 0 },
    //   //{ id: 4, ruleId: 3, subRuleName: 'Date/Time' },
    //   { id: 3, ruleId: 4, subRuleName: 'Numeric Range', subRuleId: 0 },
    //   { id: 4, ruleId: 4, subRuleName: 'Comparison Operators', subRuleId: 0 },
    //   //{ id: 7, ruleId: 5, subRuleName: 'Comparison Operators' }

    // );

    // return this.subRules;
  }
  getPatterns(subRuleId: number) {

    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIPatterns?subRuleId=${subRuleId}`;
    return this.http.get<APIResponse<Patterns[]>>(url, {
      headers: headers,
    });
  }

  getConditionalOperators() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + 'api/ProcessConfiguration/GetDIConditionalOperators';
    return this.http.get<APIResponse<ConditionalOperator[]>>(url, {
      headers: headers,
    });
  }

  getRuleSetNamesBySecGrpId(securityGroupIds: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIRuleSetNamesBySecGrpId?securityGroupId=${securityGroupIds}`;
    return this.http.get<APIResponse<RuleSetNames[]>>(url, {
      headers: headers,
    });
  }

  getRuleSetByRuleSetNameId(ruleSetNameId: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIRuleSetByRuleSetNameId?ruleSetNameId=${ruleSetNameId}`;
    return this.http.get<APIResponse<ExcelRule[]>>(url, {
      headers: headers,
    });
  }

  getRuleSetByRuleSetName(ruleSetName: string, sgIds : string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIRuleSetByRuleSetName?ruleSetName=${ruleSetName}&securityGroupId=${sgIds}`;
    return this.http.get<APIResponse<RuleSetNames[]>>(url, {
      headers: headers,
    });
  }


  getDataInsights() {
    return this.http.get(`${this.baseUrl}insights`);
  }

  getDataInsightById(id: string) {
    return this.http.get(`${this.baseUrl}insights/${id}`);
  }

  createDataInsight(data: any) {
    return this.http.post(`${this.baseUrl}insights`, data);
  }

  updateDataInsight(id: string, data: any) {
    return this.http.put(`${this.baseUrl}insights/${id}`, data);
  }

  deleteDataInsight(id: string) {
    return this.http.delete(`${this.baseUrl}insights/${id}`);
  }
  validateRule(file: File, rules: RuleType[]) {
    const formData = new FormData();
    formData.append('rules', JSON.stringify(rules));
    formData.append('file', file);
    return this.http.post(environment.apiEndpoint, formData);
  }

  getGenericRules() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIGenericRules`;
    return this.http.get<APIResponse<ExcelRule[]>>(url, {
      headers: headers,
    });
  }

  getGenericRuleNames(isActive: boolean) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/GetDIGenericRulesNames?isActive=${isActive}`;
    return this.http.get<APIResponse<RuleSetNames[]>>(url, {
      headers: headers,
    });
  }

  checkRuleSetNameIfUnique(ruleSetName: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/CheckDIRuleSetNameExists?ruleSetName=${ruleSetName}`;
    return this.http.get<APIResponse<boolean>>(url, {
      headers: headers,
    });
  }

  insertRuleSets(payLoad: any, flpConfigurationId: string, tabName : string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + `api/ProcessConfiguration/InsertRuleSets`;
    //sessionStorage.getItem('upn').split('@')[0]
    // Construct the HttpParams object
    let params = new HttpParams();
    if (flpConfigurationId) {
      params = params.set('flpConfigurationId', flpConfigurationId);
    }

    if(tabName) {
      params = params.set('tabName', tabName);
    }

    return this.http.post<APIResponse<boolean>>(url, payLoad, { headers: headers, params : params });
  }

  getRuleSetConfigList(request: RuleSetListRequest): Observable<APIResponse<RuleSetListResponse>> {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this.http.post<APIResponse<RuleSetListResponse>>(
      this.baseUrl + 'api/ProcessConfiguration/GetRuleSetNameList', request,
      { headers: headers }
    );
  }

  GenerateCustomRule(data : DataAssistsRequest) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    
    return this.http.post<APIResponse<boolean>>(
      this.baseUrl + 'api/DataAssists/GenerateResponse',
      data,
      { headers }
    );
  }

  //will get all LandlingLayer prefixes
  getPrefixes() {    
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + 'api/ProcessConfiguration/GetPrefixes';
    return this.http.get<APIResponse<Prefixes[]>>(url, {
      headers: headers,
    });

  }
}
