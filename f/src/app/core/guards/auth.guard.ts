import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { map, Observable } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { LoginService } from '../services/login.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard  {

  constructor(private loginService : LoginService, private router: Router){}
  
  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean> | Promise<boolean> | boolean {
      return this.loginService.currentUser$.pipe(
        map(auth =>
          {
            if(auth?.isAdmin) {
              return true;
            }
            else{
              this.router.navigate(['/configuration'],{queryParams : {returnUrl: state.url}});
              return false;
            }
          })
      );
    }
  
}
  
