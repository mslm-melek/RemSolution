import { ChatAuthorKind, ChatMessageDto } from '../web-api-client';

/**
 * A run of consecutive messages from one side on one day — what a bubble stack
 * is.
 *
 * Grouping is what makes a conversation read as a conversation rather than as a
 * log: the sender's name and the clock are said once per run, not once per
 * message, and only the corners between neighbours are squared off. It lives
 * here because the agency inbox and the customer's own messages are the same
 * conversation seen from the two ends, and only which side counts as "mine"
 * differs.
 */
export interface ChatBubbleGroup {
  mine: boolean;
  senderName?: string;
  /** For the avatar beside the other side's run. */
  initial: string;
  messages: ChatMessageDto[];
  /** When the run ended — the one clock shown under it. */
  sentAt?: Date;
  /** Whether the far side has read all of it. Only ever asked of my own runs. */
  read: boolean;
}

/** One calendar day of the conversation, behind its date separator. */
export interface ChatBubbleDay {
  day: Date;
  groups: ChatBubbleGroup[];
}

/**
 * Same author and same day, or the run ends. Deliberately not also capped by
 * elapsed time: the separator already carries the day, and a forty-minute gap
 * inside one afternoon is still one exchange.
 *
 * `sentAt` is an instant, so the day is the READER's day — which is the day the
 * timestamps beside it are showing (see PROJECT_OVERVIEW §4).
 */
export function groupChatMessages(
  messages: ChatMessageDto[], mine: ChatAuthorKind): ChatBubbleDay[] {
  const days: ChatBubbleDay[] = [];

  for (const message of messages) {
    const isMine = message.authorKind === mine;
    let day = days[days.length - 1];

    if (!day || !isSameDay(day.day, message.sentAt)) {
      day = { day: message.sentAt ?? new Date(), groups: [] };
      days.push(day);
    }

    let group = day.groups[day.groups.length - 1];

    if (!group || group.mine !== isMine) {
      group = {
        mine: isMine,
        senderName: message.senderName,
        initial: initialOf(message.senderName),
        messages: [],
        read: true
      };
      day.groups.push(group);
    }

    group.messages.push(message);
    group.sentAt = message.sentAt ?? group.sentAt;
    // A run counts as read only once every message in it has been.
    group.read = group.read && !!message.readAt;
  }

  return days;
}

/**
 * Groups a conversation, and regroups only when the message array is REPLACED.
 *
 * The template asks for this on every change-detection pass. Regrouping each
 * time would hand `*ngFor` a fresh object graph every pass and rebuild every
 * bubble in the DOM — losing the scroll position along with it. Both components
 * replace `messages` rather than pushing into it, so identity is a sound test.
 */
export class ChatBubbles {
  private days: ChatBubbleDay[] = [];
  private from: ChatMessageDto[] | null = null;

  constructor(private readonly mine: ChatAuthorKind) { }

  of(messages: ChatMessageDto[]): ChatBubbleDay[] {
    if (messages !== this.from) {
      this.from = messages;
      this.days = groupChatMessages(messages, this.mine);
    }

    return this.days;
  }
}

function isSameDay(a: Date, b?: Date): boolean {
  return !!b
    && a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

function initialOf(name?: string): string {
  return (name ?? '').trim().charAt(0).toUpperCase() || '?';
}
