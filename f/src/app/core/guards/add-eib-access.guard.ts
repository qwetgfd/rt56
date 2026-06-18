


import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class addEIBAccessGuard implements CanActivate {
  constructor(private router: Router) { }

  canActivate(): boolean {
    const navigation = this.router.getCurrentNavigation();
    const state = navigation?.extras?.state;


    if (state && (state['fromCreateEIB'])) {
      return true;
    }


    // Redirect to CreateRule if accessed directly
    this.router.navigate(['/eib']);
    return false;
  }
}