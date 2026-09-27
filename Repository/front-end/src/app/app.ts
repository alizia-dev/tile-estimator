import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Root component. It renders only the router outlet: the signed-in shell and the
 * customer-facing quote page each bring their own chrome, so neither inherits the other's.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
