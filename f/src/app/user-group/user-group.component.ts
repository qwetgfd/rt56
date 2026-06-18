import { Component, OnInit } from '@angular/core';
import { AuthService } from '../core/services/auth.service';
import { timeout } from 'rxjs';

@Component({
    selector: 'app-user-group',
    templateUrl: './user-group.component.html',
    styleUrl: './user-group.component.css',
    standalone: false
})
export class UserGroupComponent implements OnInit {
  constructor(private authService: AuthService){}
  ngOnInit(): void {
    setTimeout(() => {
      this.authService.logout();
    }, 10000);
  }

}
