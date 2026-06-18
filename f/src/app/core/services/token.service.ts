import { isPlatformBrowser } from '@angular/common';
import { Inject, Injectable, OnDestroy, PLATFORM_ID } from '@angular/core';
import { BehaviorSubject, filter, Observable, take } from 'rxjs';

type TokenMessage = { type: 'SET_TOKEN'; token: string | null };
@Injectable({
  providedIn: 'root'
})
export class TokenService implements OnDestroy {
  private tokenInMemory: string | null = null;
  private channel?: BroadcastChannel;
  private storageKey = 'di-api-token';
  private storageEventHandler = (e: StorageEvent) => {
    if (e.key === this.storageKey) {
      const newToken = e.newValue ? JSON.parse(e.newValue) as string : null;

    }
  }
  private tokenSubject = new BehaviorSubject<string | null>(null);

  /** Emits current token and subsequent changes */
  readonly token$ = this.tokenSubject.asObservable().pipe(
    filter((token): token is string => token !== null)
  );

  readonly tokenReady$ = this.tokenSubject.asObservable().pipe(
    filter((token): token is string => token !== null),
    take(1)
  );

  constructor(@Inject(PLATFORM_ID) private platformId: Object) {
    const isBrowser = isPlatformBrowser(this.platformId);
    if (!isBrowser) return;

    // Initialize from storage
    const persisted = localStorage.getItem(this.storageKey);
    if (persisted) {
      this.tokenInMemory = JSON.parse(persisted);
      this.tokenSubject.next(this.tokenInMemory);
    }

    // Prefer BroadcastChannel (no 'in window' — avoids narrowing bug)
    const hasBC = typeof BroadcastChannel !== 'undefined';
    if (hasBC) {
      this.channel = new BroadcastChannel('di-api-token');
      this.channel.onmessage = (e: MessageEvent<TokenMessage>) => {
        if (e.data?.type === 'SET_TOKEN') {
          this.applyIncomingToken(e.data.token, 'broadcast');
        }
      };
    } else if (typeof window !== 'undefined') {
      // TS knows window is present now
      (window as Window).addEventListener('storage', this.storageEventHandler);
    }
  }


  /** Set (or clear) the token and broadcast to other tabs */
  setToken(token: string | null): void {
    this.tokenInMemory = token;
    this.tokenSubject.next(token);

    // Persist (optional, enables fallback and reload resilience)
    if (isPlatformBrowser(this.platformId)) {
      if (token) {
        localStorage.setItem(this.storageKey, JSON.stringify(token));
      } else {
        localStorage.removeItem(this.storageKey);
      }
    }

    // Cross-tab notify
    if (this.channel) {
      this.channel.postMessage({ type: 'SET_TOKEN', token });
    } else if (isPlatformBrowser(this.platformId)) {
      // Trigger storage event on *other tabs* by changing localStorage
      // (already done above). Some apps also write a timestamp to ensure event fires.
      // localStorage.setItem(this.storageKey + ':touch', Date.now().toString());
    }
  }



  /** Get the latest token synchronously (for interceptors) */
  getToken(): string | null {
    return this.tokenInMemory;
  }


  ngOnDestroy(): void {
    if (this.channel) {
      this.channel.close();
    } else if (isPlatformBrowser(this.platformId)) {
      window.removeEventListener('storage', this.storageEventHandler);
    }
    this.tokenSubject.complete();

  }


  private applyIncomingToken(token: string | null, from: 'broadcast' | 'storage') {
    //avoid unneceasry next() calls
    if (this.tokenInMemory !== token) {
      this.tokenInMemory = token;
      this.tokenSubject.next(token);

      //keep storage in sync
      if (isPlatformBrowser(this.platformId)) {
        if (token) {
          localStorage.setItem(this.storageKey, JSON.stringify(token));
        } else {
          localStorage.removeItem(this.storageKey);
        }
      }
    }
  }


}
