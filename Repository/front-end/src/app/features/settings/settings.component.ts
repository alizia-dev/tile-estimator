import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { OrganizationApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Invitation, Member, OrganizationSettings } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { ShortDatePipe } from '../../shared/pipes';

const ROLES = ['Owner', 'Admin', 'Estimator', 'Sales', 'Installer', 'Viewer'] as const;

@Component({
  selector: 'te-settings',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDialogModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    ShortDatePipe,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  private readonly api = inject(OrganizationApi);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly roles = ROLES;

  readonly settings = signal<OrganizationSettings | null>(null);
  readonly members = signal<Member[]>([]);
  readonly invitations = signal<Invitation[]>([]);

  readonly loading = signal(true);
  readonly saving = signal(false);

  readonly memberColumns = ['name', 'email', 'role', 'status', 'lastLogin', 'actions'];
  readonly invitationColumns = ['email', 'role', 'expires', 'status', 'actions'];

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    legalName: [''],
    email: ['', Validators.email],
    phone: [''],
    website: [''],
    licenseNumber: [''],
    line1: [''],
    city: [''],
    state: [''],
    postalCode: [''],
  });

  readonly pricingForm = this.fb.nonNullable.group({
    defaultTaxRatePercentage: [0, Validators.min(0)],
    taxBasis: ['MaterialsAndLabor'],
    defaultOverheadPercentage: [0, Validators.min(0)],
    defaultPricingStrategy: ['Markup'],
    defaultMarkupPercentage: [20, Validators.min(0)],
    defaultMarginPercentage: [25, [Validators.min(0), Validators.max(99.99)]],
    discountBeforeTax: [true],
    quoteValidityDays: [30, [Validators.required, Validators.min(1)]],
    quoteTermsAndConditions: [''],
    quoteFooterNote: [''],
    customerNumberPrefix: ['CUST'],
    projectNumberPrefix: ['PRJ'],
    estimateNumberPrefix: ['EST'],
    quoteNumberPrefix: ['QTE'],
    changeOrderNumberPrefix: ['CO'],
  });

  readonly inviteForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    roleName: ['Estimator', Validators.required],
  });

  constructor() {
    this.loadAll();
  }

  private loadAll(): void {
    this.loading.set(true);

    forkJoin({
      settings: this.api.getSettings(),
      members: this.api.listMembers(),
      invitations: this.api.listInvitations(),
    }).subscribe({
      next: (data) => {
        this.apply(data.settings);
        this.members.set(data.members);
        this.invitations.set(data.invitations);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private apply(settings: OrganizationSettings): void {
    this.settings.set(settings);

    this.profileForm.patchValue({
      name: settings.organizationName,
      legalName: settings.legalName ?? '',
      email: settings.email ?? '',
      phone: settings.phone ?? '',
      website: settings.website ?? '',
      licenseNumber: settings.licenseNumber ?? '',
      line1: settings.address?.line1 ?? '',
      city: settings.address?.city ?? '',
      state: settings.address?.state ?? '',
      postalCode: settings.address?.postalCode ?? '',
    });

    this.pricingForm.patchValue({
      defaultTaxRatePercentage: settings.defaultTaxRatePercentage,
      taxBasis: settings.taxBasis,
      defaultOverheadPercentage: settings.defaultOverheadPercentage,
      defaultPricingStrategy: settings.defaultPricingStrategy,
      defaultMarkupPercentage: settings.defaultMarkupPercentage,
      defaultMarginPercentage: settings.defaultMarginPercentage,
      discountBeforeTax: settings.discountBeforeTax,
      quoteValidityDays: settings.quoteValidityDays,
      quoteTermsAndConditions: settings.quoteTermsAndConditions ?? '',
      quoteFooterNote: settings.quoteFooterNote ?? '',
      customerNumberPrefix: settings.customerNumberPrefix,
      projectNumberPrefix: settings.projectNumberPrefix,
      estimateNumberPrefix: settings.estimateNumberPrefix,
      quoteNumberPrefix: settings.quoteNumberPrefix,
      changeOrderNumberPrefix: 'CO',
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const value = this.profileForm.getRawValue();

    const address =
      value.line1 && value.city && value.state && value.postalCode
        ? {
            line1: value.line1,
            line2: null,
            city: value.city,
            state: value.state.toUpperCase(),
            postalCode: value.postalCode,
            country: 'US',
          }
        : null;

    this.saving.set(true);

    this.api
      .updateProfile({
        name: value.name,
        legalName: value.legalName || null,
        email: value.email || null,
        phone: value.phone || null,
        website: value.website || null,
        licenseNumber: value.licenseNumber || null,
        address,
      })
      .subscribe({
        next: (settings) => {
          this.apply(settings);
          this.saving.set(false);
          this.snackBar.open('Company details saved.', 'Dismiss', { duration: 3500 });
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.snackBar.open(errorMessage(err, 'That could not be saved.'), 'Dismiss', {
            duration: 7000,
          });
        },
      });
  }

  savePricing(): void {
    if (this.pricingForm.invalid) {
      this.pricingForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.api.updateSettings(this.pricingForm.getRawValue()).subscribe({
      next: (settings) => {
        this.apply(settings);
        this.saving.set(false);
        this.snackBar.open(
          'Defaults saved. Estimates already in progress keep the settings they were created with.',
          'Dismiss',
          { duration: 6000 },
        );
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'That could not be saved.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }

  // --- Members -----------------------------------------------------------------------------

  changeRole(member: Member, roleName: string): void {
    if (roleName === member.roleName) return;

    this.api.updateMember(member.membershipId, roleName, member.isActive).subscribe({
      next: () => {
        this.api.listMembers().subscribe((members) => this.members.set(members));
        this.snackBar.open(`${member.firstName} is now a ${roleName}.`, 'Dismiss', { duration: 4000 });
      },
      error: (err: unknown) =>
        this.snackBar.open(errorMessage(err, 'The role could not be changed.'), 'Dismiss', {
          duration: 7000,
        }),
    });
  }

  toggleActive(member: Member): void {
    const activating = !member.isActive;

    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: activating ? `Reactivate ${member.firstName}?` : `Deactivate ${member.firstName}?`,
          message: activating
            ? 'They will be able to sign in to this organization again.'
            : 'They will lose access to this organization. Their work stays exactly as it is.',
          confirmLabel: activating ? 'Reactivate' : 'Deactivate',
          destructive: !activating,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.api.updateMember(member.membershipId, member.roleName, activating).subscribe({
          next: () => this.api.listMembers().subscribe((members) => this.members.set(members)),
          error: (err: unknown) =>
            this.snackBar.open(errorMessage(err, 'That could not be changed.'), 'Dismiss', {
              duration: 7000,
            }),
        });
      });
  }

  invite(): void {
    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }

    const value = this.inviteForm.getRawValue();
    this.saving.set(true);

    this.api.invite(value.email, value.roleName).subscribe({
      next: () => {
        this.saving.set(false);
        this.inviteForm.reset({ email: '', roleName: 'Estimator' });
        this.api.listInvitations().subscribe((invitations) => this.invitations.set(invitations));
        this.snackBar.open(`Invitation sent to ${value.email}.`, 'Dismiss', { duration: 5000 });
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'The invitation could not be sent.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }

  revokeInvitation(invitation: Invitation): void {
    this.api.revokeInvitation(invitation.id).subscribe({
      next: () => this.api.listInvitations().subscribe((invitations) => this.invitations.set(invitations)),
    });
  }
}
