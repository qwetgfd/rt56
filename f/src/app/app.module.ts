import { NgModule, NO_ERRORS_SCHEMA } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgbDatepickerModule } from '@ng-bootstrap/ng-bootstrap';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { CoreModule } from './core/core.module';
import { SharedModule } from './shared/shared.module';
import { MainLayoutModule } from './main-layout/main-layout.module';

import { NgSelectModule } from '@ng-select/ng-select';
import { NgApexchartsModule } from 'ng-apexcharts';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';

import { LoginComponent } from './login/login.component';
import { UnauthorizedComponent } from './unauthorized/unauthorized.component';
import { FileUploadComponent } from './file-upload/file-upload.component';
import { FileProcessingStatusComponent } from './file-processing-status/file-processing-status.component';
import { FileStatusChartComponent } from './file-status-chart/file-status-chart.component';
import { ProcessStatusTemplateComponent } from './process-status-template/process-status-template.component';
import { ProcessConfigurationComponent } from './process-configuration/process-configuration.component';
import { AddProcessComponent } from './add-process/add-process.component';
import { ProcessConfigListComponent } from './process-config-list/process-config-list.component';
import { NewProcessConfigurationComponent } from './new-process-configuration/new-process-configuration.component';
import { ConfigurationFirstSharepointTabComponent } from './new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component';
import { ConfigurationFirstSharepointTabNavComponent } from './new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component';
import { FilePreviewComponent } from './new-process-configuration/file-preview/file-preview.component';
import { ProcessedFileListComponent } from './processed-file-list/processed-file-list.component';
import { DatasourcesComponent } from './datasources/datasources.component';
import { UserGroupComponent } from './user-group/user-group.component';
import { RulesetListComponent } from './ruleset-list/ruleset-list.component';
import { CreateRuleComponent } from './create-rule/create-rule.component';
import { RegexBuilderComponent } from './regex-builder/regex-builder.component';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { DiRegionComponent } from './di-region/di-region.component';
import { DiUploadsComponent } from './di-uploads/di-uploads.component';
import { DiUtilizationComponent } from './di-utilization/di-utilization.component';
import { CreateEibComponent } from './eib/create-eib/create-eib.component';

import { MsalInterceptor, MsalModule } from '@azure/msal-angular';
import { GuestInterceptor } from './core/interceptors/guest.interceptor';
import { LoadingInterceptor } from './core/interceptors/loading.interceptor';
import { ErrorInterceptor } from './core/interceptors/error.interceptor';
import {
  MSALGuardConfigFactory,
  MSALInstanceFactory,
  MSALInterceptorConfigFactory,
} from './core/config/msal.config';

// #region Sharepoint Workspace - AY
import { SharepointComponent } from './sharepoint/sharepoint.component';
import { SharepointWorkspaceComponent } from './sharepoint/sharepoint-workspace/sharepoint-workspace.component';
import { FileFirstSharepointAttachComponent } from './file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component';
import { SHAREPOINT_ENV } from './sharepoint/core/sharepoint.config';
import { environment } from './environments/environment';
// #endregion

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    UnauthorizedComponent,
    FileUploadComponent,
    FileProcessingStatusComponent,
    FileStatusChartComponent,
    ProcessStatusTemplateComponent,
    ProcessConfigurationComponent,
    AddProcessComponent,
    ProcessConfigListComponent,
    NewProcessConfigurationComponent,
    ConfigurationFirstSharepointTabComponent,
    ConfigurationFirstSharepointTabNavComponent,
    FilePreviewComponent,
    ProcessedFileListComponent,
    DatasourcesComponent,
    UserGroupComponent,
    RulesetListComponent,
    CreateRuleComponent,
    RegexBuilderComponent,
    ConfirmDialogComponent,
    DashboardComponent,
    DiRegionComponent,
    DiUploadsComponent,
    DiUtilizationComponent,
    CreateEibComponent,
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    AppRoutingModule,
    CoreModule,
    SharedModule,
    MainLayoutModule,
    FormsModule,
    ReactiveFormsModule,
    NgSelectModule,
    NgApexchartsModule,
    TableModule,
    ButtonModule,
    DatePicker,
    PaginatorModule,
    NgbDatepickerModule,
    MsalModule.forRoot(
      MSALInstanceFactory(),
      MSALGuardConfigFactory(),
      MSALInterceptorConfigFactory()
    ),
    // #region Sharepoint Workspace - AY
    SharepointComponent,
    FileFirstSharepointAttachComponent,
    SharepointWorkspaceComponent,
    // #endregion
  ],
  providers: [
    provideHttpClient(withInterceptorsFromDi()),
    // #region Sharepoint Workspace - AY
    { provide: HTTP_INTERCEPTORS, useClass: GuestInterceptor, multi: true },
    // #endregion
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: LoadingInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
    // #region Sharepoint Workspace - AY
    {
      provide: SHAREPOINT_ENV,
      useValue: {
        production: environment.production,
        apiBaseUrl: environment.sharepointApiBaseUrl,
        apiVersion: environment.apiVersion,
        ownerKey: environment.ownerKey,
        defaultLibraryName: environment.defaultLibraryName,
        tenantName: environment.tenantName,
        clientId: environment.clientId,
        authority: environment.authority,
        hostName: environment.hostName,
        siteName: environment.siteName,
        libraryName: environment.libraryName,
        userGraphScopes: environment.userGraphScopes,
      },
    },
    // #endregion
  ],
  schemas: [NO_ERRORS_SCHEMA],
  bootstrap: [AppComponent],
})
export class AppModule {}