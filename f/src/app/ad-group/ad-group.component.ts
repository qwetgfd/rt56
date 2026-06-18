import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { SecurityGroup } from '../core/models/userDetails';
import { ToastrService } from 'ngx-toastr';
@Component({
    selector: 'app-ad-group',
    templateUrl: './ad-group.component.html',
    styleUrl: './ad-group.component.css',
    standalone: false
})
export class AdGroupComponent implements OnInit {
  @Input() securityGroup: SecurityGroup[];
  @Output() groupId = new EventEmitter<string>();
  @Output() closeModal = new EventEmitter<void>();
  securityGroupId: string;
  previousGroupId: string;
  constructor(private toastr: ToastrService) {}
  ngOnInit(): void {
    this.securityGroupId = sessionStorage.getItem('GUID');
    this.previousGroupId= sessionStorage.getItem('GUID');
  }
  updateGroup() {
    // if(this.securityGroupId == sessionStorage.getItem('GUID')){
    //   this.toastr.info("")
    // }
    const confirmed = window.confirm(
      'Are you sure you want to change the security group?'
    );
    if (confirmed) {
      this.groupId.emit(this.securityGroupId);
      this.closeModal.emit();
    }
  }
  close() {
    this.closeModal.emit();
  }
}
