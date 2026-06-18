import { inject, Pipe, PipeTransform } from "@angular/core";
import { FabService } from "../../core/services/FAB/fab-service.service";

@Pipe({
    name: 'keys', pure: true,
    standalone: false
})
export class KeysPipe implements PipeTransform{
    transform(value: object): string[] {
        return Object.keys(value);
    }
}


@Pipe({
  name: 'isFabUser',
  standalone: false,
  pure: false, // re-run if service value can change at runtime
})
export class IsFabUserPipe implements PipeTransform {
  private nav = inject(FabService);

  transform(_value? : unknown): boolean {
    // If isFABUser is a boolean, return it directly.
    // If it's an Observable/Signal, adapt accordingly (see notes below).
    return !!this.nav.isFABUser$;
  }
}
