import {
  Directive,
  effect,
  inject,
  input,
  TemplateRef,
  ViewContainerRef,
} from '@angular/core';

import { AuthService } from '../core/auth/auth.service';

/**
 * Structural directive that shows its content only when the user holds one of the given
 * permissions:
 *
 * ```html
 * <button *teHasPermission="'estimate.update'">Edit</button>
 * <div *teHasPermission="['quote.send', 'quote.approve']">…</div>
 * ```
 *
 * This hides UI so people are not shown buttons that would fail. It is not a security control:
 * the same permission is enforced server-side on every endpoint.
 */
@Directive({
  selector: '[teHasPermission]',
})
export class HasPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly auth = inject(AuthService);

  readonly teHasPermission = input.required<string | string[]>();

  private rendered = false;

  constructor() {
    effect(() => {
      const required = this.teHasPermission();
      const permissions = Array.isArray(required) ? required : [required];
      const allowed = this.auth.hasAny(...permissions);

      if (allowed && !this.rendered) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
