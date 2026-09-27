import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

interface NavItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
  /** The permission that makes this destination usable. Hidden when the user lacks it. */
  readonly permission?: string;
}

/**
 * The application shell: navigation, organization switcher and user menu.
 *
 * Desktop-first, as a contractor is usually estimating at a desk, but the nav collapses to an
 * overlay drawer on a tablet so the same screens work on site (SPEC 21).
 */
@Component({
  selector: 'te-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule,
    MatTooltipModule,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  private readonly auth = inject(AuthService);

  readonly user = this.auth.user;
  readonly displayName = this.auth.displayName;
  readonly initials = this.auth.initials;
  readonly organizations = this.auth.organizations;

  readonly sidenavOpen = signal(true);

  private readonly allNavItems: readonly NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Projects', icon: 'folder', route: '/projects', permission: 'project.read' },
    { label: 'Estimates', icon: 'calculate', route: '/estimates', permission: 'estimate.read' },
    { label: 'Quotes', icon: 'request_quote', route: '/quotes', permission: 'quote.read' },
    { label: 'Customers', icon: 'people', route: '/customers', permission: 'customer.read' },
    { label: 'Catalog', icon: 'inventory_2', route: '/catalog', permission: 'catalog.read' },
    { label: 'Calculators', icon: 'straighten', route: '/calculators', permission: 'estimate.read' },
    { label: 'Reports', icon: 'assessment', route: '/reports', permission: 'reports.read' },
    { label: 'Settings', icon: 'settings', route: '/settings', permission: 'settings.read' },
  ];

  readonly navItems = computed(() =>
    this.allNavItems.filter((item) => !item.permission || this.auth.has(item.permission)),
  );

  /** Shown only when the user actually belongs to more than one organization. */
  readonly showOrganizationSwitcher = computed(() => this.organizations().length > 1);

  toggleSidenav(): void {
    this.sidenavOpen.update((open) => !open);
  }

  switchOrganization(organizationId: string): void {
    if (organizationId === this.user()?.activeOrganizationId) {
      return;
    }

    // Reload so every open list re-queries under the new tenant rather than showing stale rows.
    this.auth.switchOrganization(organizationId).subscribe({
      next: () => window.location.reload(),
    });
  }

  logout(): void {
    this.auth.logout();
  }
}
