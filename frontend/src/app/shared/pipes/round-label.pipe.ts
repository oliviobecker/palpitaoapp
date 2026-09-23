import { Pipe, PipeTransform } from '@angular/core';
import { roundLabel } from '../utils/round-name.util';

/**
 * A round's label in templates: `{{ round.number | roundLabel: round.part }}` → "10", or "10.2"
 * for part 2 of a round played in parts. Pure, so it only reruns when the inputs change.
 */
@Pipe({ name: 'roundLabel' })
export class RoundLabelPipe implements PipeTransform {
  transform(number: number | null | undefined, part?: number | null): string {
    return number == null ? '' : roundLabel(number, part);
  }
}
