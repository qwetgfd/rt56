import { Component, Input, OnInit } from '@angular/core';
import { ProfilingSPNames } from '../../core/models/EIB/dbView';
import { EibService } from '../../core/services/eib/eib.service';
import { APIResponse } from '../../core/models/apiResponse';
import { TablePageEvent } from 'primeng/table';

@Component({
  selector: 'app-profiling-details',
  standalone: false,
  templateUrl: './profiling-details.component.html',
  styleUrl: './profiling-details.component.css'
})
export class ProfilingDetailsComponent implements OnInit {
  //@Input() profilingRunHistoryLog: ProfilingRunLogHistory[] = [];
  @Input() procedureNameId: number = 0;
  spNameDetails : ProfilingSPNames;
  searchLoading: boolean = false;
  /**
   *
   */
  constructor(private eibService: EibService) {


  }
  ngOnInit(): void {
    console.log('procedureNameId:',this.procedureNameId)
    if (this.procedureNameId > 0) {
      this.searchLoading = true;
      this.eibService.GetCurrentStatusOfPDMSP(this.procedureNameId).subscribe({
        next: (response: APIResponse<ProfilingSPNames>) => {
          if (response?.responseCode === 200 && response.result) {
            this.spNameDetails = response.result;
            this.searchLoading = false;            
          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            // Optionally show a message to the user or handle fallback logic here
            this.searchLoading = false;
          }
        }, error: error => {          
          console.error("Error retrieving Profiling SP Configuration");
          this.searchLoading = false;
        }
      });
    }
  }

  rows = 5;
  first = 0;

  onPage(event: TablePageEvent){
    

    // If the user changed the rows-per-page, reset to page 1.
    if (event.rows !== this.rows) {
      this.rows = event.rows;
      this.first = 0; // go back to first page
      return;
    }

    // Otherwise, just track normal page changes
    this.first = event.first;
  }

}
