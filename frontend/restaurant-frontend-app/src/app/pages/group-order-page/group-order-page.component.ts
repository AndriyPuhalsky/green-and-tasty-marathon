import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import {
  catchError,
  EMPTY,
  exhaustMap,
  finalize,
  forkJoin,
  interval,
  map,
  startWith,
  Subscription,
  switchMap,
  takeWhile,
  tap,
} from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { GROUP_STATUS_LABELS, SYNC_INTERVAL_MS } from './constants';
import { GroupOrderLoadState } from './models';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ModalComponent, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly groupOrders = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);

  private readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  private syncSubscription?: Subscription;

  readonly state = signal<GroupOrderLoadState>('loading');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly status = signal<GroupReservationStatus>('Open');
  readonly submitDialogOpen = signal(false);
  readonly submitting = signal(false);
  readonly guestToRemove = signal<Participant | null>(null);
  readonly removing = signal(false);
  readonly lastUpdated = signal<Date | null>(null);
  readonly syncing = signal(false);
  readonly syncFailed = signal(false);

  readonly groupId = computed(() => this.invite()?.GroupReservationId ?? '');
  readonly isOpen = computed(() => this.status() === 'Open');
  readonly isSubmitted = computed(() => this.status() === 'Submitted');
  readonly statusLabel = computed(() => GROUP_STATUS_LABELS[this.status()]);

  // у замовлення ресторану йдуть лише підтверджені кошики
  readonly order = computed(() => consolidatePreOrder(this.participants()));
  readonly isEmpty = computed(() => this.participants().every((person) => !person.items.length));
  readonly canSubmit = computed(
    () => this.isOpen() && this.order().items.length > 0 && !this.submitting()
  );

  readonly guestsLabel = computed(
    () => `${this.participants().length} of ${this.invite()?.MaxGuestCount ?? 0} guests`
  );

  readonly dishesLabel = computed(() => {
    const count = this.order().items.reduce((sum, item) => sum + item.quantity, 0);
    return `${count} ${count === 1 ? 'dish' : 'dishes'}`;
  });

  readonly pendingNotice = computed(() => {
    const count = this.participants().filter((person) => !person.isSubmitted).length;
    if (!this.isOpen() || count === 0) {
      return '';
    }
    const who = count === 1 ? "1 participant hasn't" : `${count} participants haven't`;
    return `${who} submitted their cart yet — their dishes won't be included in the order`;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.stopSync();
    this.state.set('loading');

    this.groupOrders
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) =>
          forkJoin({
            sessions: this.groupOrders.getSessions$(invite.GroupReservationId),
            ownerSessionId: this.groupOrders.getOwnerSessionId$(this.reservationId),
            status: this.groupOrders.getStatus$(invite.GroupReservationId),
          }).pipe(map((data) => ({ invite, ...data })))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ({ invite, sessions, ownerSessionId, status }) => {
          this.invite.set(invite);
          this.participants.set(sessions.participants);
          this.ownerSessionId.set(ownerSessionId);
          this.status.set(status.status);
          this.lastUpdated.set(new Date());
          this.syncFailed.set(false);
          this.state.set('ready');
          this.startSync();
        },
        error: (error: HttpErrorResponse) => {
          this.state.set(error.status === 404 ? 'not-found' : 'error');
        },
      });
  }

  openSubmitDialog(): void {
    this.submitDialogOpen.set(true);
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) {
      this.submitDialogOpen.set(false);
    }
  }

  submit(): void {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);

    this.groupOrders
      .submit$(this.groupId(), this.reservationId)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.stopSync();
          this.status.set('Submitted');
          this.submitDialogOpen.set(false);
          this.toast.success('Pre-order sent to the restaurant');
        },
        // текст помилки вже показав tokenInterceptor
        error: () => this.submitDialogOpen.set(false),
      });
  }

  askRemove(guest: Participant): void {
    this.guestToRemove.set(guest);
  }

  closeRemoveDialog(): void {
    if (!this.removing()) {
      this.guestToRemove.set(null);
    }
  }

  removeGuest(): void {
    const guest = this.guestToRemove();
    if (!guest || this.removing()) {
      return;
    }
    this.removing.set(true);

    this.groupOrders
      .removeSession$(this.groupId(), guest.id)
      .pipe(
        finalize(() => this.removing.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (sessions) => {
          this.participants.set(sessions.participants);
          this.lastUpdated.set(new Date());
          this.guestToRemove.set(null);
          this.toast.success(`${guest.name} was removed from the reservation`);
        },
        error: () => this.guestToRemove.set(null),
      });
  }

  refresh(): void {
    if (!this.syncing()) {
      this.startSync(true);
    }
  }

  // один таймер на сторінку: кожен старт спершу гасить попередній
  private startSync(now = false): void {
    this.stopSync();
    if (!this.isOpen()) {
      return;
    }

    const ticks$ = now ? interval(SYNC_INTERVAL_MS).pipe(startWith(0)) : interval(SYNC_INTERVAL_MS);

    this.syncSubscription = ticks$
      .pipe(
        // група вже не Open (наприклад, відправили з іншої вкладки), синхронізувати нічого
        takeWhile(() => this.isOpen()),
        // поки попередній запит не відповів, новий не стартує
        exhaustMap(() => this.fetchLatest$()),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  private stopSync(): void {
    this.syncSubscription?.unsubscribe();
    this.syncSubscription = undefined;
  }

  private fetchLatest$() {
    const groupId = this.groupId();
    this.syncing.set(true);

    return forkJoin({
      sessions: this.groupOrders.getSessions$(groupId, { silent: true }),
      status: this.groupOrders.getStatus$(groupId, { silent: true }),
    }).pipe(
      tap(({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.status.set(status.status);
        this.lastUpdated.set(new Date());
        this.syncFailed.set(false);
      }),
      // лишаємо останні дані, а таймер працює далі
      catchError(() => {
        this.syncFailed.set(true);
        return EMPTY;
      }),
      finalize(() => this.syncing.set(false))
    );
  }
}
