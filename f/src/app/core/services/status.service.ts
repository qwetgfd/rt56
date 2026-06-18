import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { EIBGenerationStatus } from '../models/EIB/dbView';

@Injectable({
  providedIn: 'root'
})
export class StatusService {
  private baseUrl = environment.apiEndpoint;
  constructor() { }
  private isConnected = false;
  private hubConnection: signalR.HubConnection;

  public startConnection(): Promise<void> {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.baseUrl}statusHub`, {
        accessTokenFactory: () => {
          return localStorage.getItem('DIApiToken');
        }
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();


    this.hubConnection.onreconnected((newConnectionId) => {
      console.log("Reconnected, new connectionId:", newConnectionId);
    });


    // If the client hasn't received *any* message for this long, it drops the connectio
    this.hubConnection.serverTimeoutInMilliseconds = 120_000; //120s


    // Client pings the server periodically to keep the transport alive.
    this.hubConnection.keepAliveIntervalInMilliseconds = 15_000;


    return this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Connected');
        this.isConnected = true;

        console.log("Connection Id: ", this.hubConnection.connectionId);
      })
      .catch(err => {
        this.isConnected = false;
        console.log('Error while startig connectionn: ' + err);
        throw err;
      });
  }


  public waitUntilConnected(): Promise<void> {
    return new Promise(resolve => {
      const check = () => {
        if (this.hubConnection?.connectionId) {
          resolve();
        } else {
          setTimeout(check, 100);
        }
      };
      check();
    });
  }


  public addStatusListener<T = unknown>(eventName: string, callback: (status: T) => void): void {
    if (!this.hubConnection) return;
    this.hubConnection.on(eventName, callback);
  }

  //removes all listeners for an event
  public removeStatusListener(eventName: string): void {
    if (!this.hubConnection) return;
    this.hubConnection.off(eventName);
  }

  public async stopConnection(): Promise<void> {
    if (this.hubConnection && this.isConnected) {
      await this.hubConnection.stop(); //cancels rreconnection and closes transport
      this.isConnected = false;

      console.log('SignalR disconected');

      //optional: clear handlers to prevent leaks if you recreate connection later
      this.hubConnection = undefined;

    }
  }


  public getConnectionId(): string | null {
    return this.hubConnection?.connectionId ?? null;
  }


}
