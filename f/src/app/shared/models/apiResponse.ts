export interface APIResponse<T>{
    responseCode : Number;
    result : T;
    responseMessage : string[];
}