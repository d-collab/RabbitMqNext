namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	

	public class BasicProperties : IDisposable, ICloneable
	{
		public static readonly BasicProperties Empty = new BasicProperties(isFrozen: true, reusable: false);

		private const byte FrozenMask = 0x01;
		private const byte ReusableMask = 0x02; // vs ephemeral. eg came from objpool instance of direct allocation

		private readonly byte _options = 0;

		// 0x01 is reserved for continuation flag. 
		private const ushort ContentTypePresence = 1 << 15;
		private const ushort ContentEncodingPresence = 1 << 14;
		private const ushort HeadersPresence = 1 << 13;
		private const ushort DeliveryModePresence = 1 << 12;
		private const ushort PriorityPresence = 1 << 11;
		private const ushort CorrelationIdPresence = 1 << 10;
		private const ushort ReplyToPresence = 1 << 9;
		private const ushort ExpirationPresence = 1 << 8;
		private const ushort MessageIdPresence = 1 << 7;
		private const ushort TimestampPresence = 1 << 6;
		private const ushort TypePresence = 1 << 5;
		private const ushort UserIdPresence = 1 << 4;
		private const ushort AppIdPresence = 1 << 3;
		private const ushort ClusterIdPresence = 1 << 2;

		internal ushort _presenceSWord = 0;

		private IDictionary<string, object> _headers;
		private AmqpTimestamp? _timestamp;
		private byte _deliveryMode;
		private byte _priority;
		private string _contentType;
		private string _contentEncoding;
		private string _correlationId;
		private string _replyTo;
		private string _expiration;
		private string _messageId;
		private string _type;
		private string _userId;
		private string _appId;
		private string _clusterId;

		public BasicProperties() : this(isFrozen: false, reusable: false)
		{
		}

		internal BasicProperties(bool isFrozen, bool reusable)
		{
			_options = (byte) ((isFrozen ? FrozenMask : 0) | (reusable ? ReusableMask : 0));

			_headers = new Dictionary<string, object>(StringComparer.Ordinal);
		}

		public bool IsContentTypePresent
		{
			get { return (_presenceSWord & ContentTypePresence) != 0; }
			internal set { _presenceSWord |= value ? ContentTypePresence : (ushort)0; }
		}

		public bool IsContentEncodingPresent
		{
			get { return (_presenceSWord & ContentEncodingPresence) != 0; }
			internal set { _presenceSWord |= value ? ContentEncodingPresence : (ushort)0; }
		}

		public bool IsHeadersPresent
		{
			get { return (_presenceSWord & HeadersPresence) != 0; }
			internal set { _presenceSWord |= value ? HeadersPresence : (ushort)0; }
		}

		public bool IsDeliveryModePresent
		{
			get { return (_presenceSWord & DeliveryModePresence) != 0; }
			internal set { _presenceSWord |= value ? DeliveryModePresence : (ushort)0; }
		}

		public bool IsPriorityPresent
		{
			get { return (_presenceSWord & PriorityPresence) != 0; }
			internal set { _presenceSWord |= value ? PriorityPresence : (ushort)0; }
		}

		public bool IsCorrelationIdPresent
		{
			get { return (_presenceSWord & CorrelationIdPresence) != 0; }
			internal set { _presenceSWord |= value ? CorrelationIdPresence : (ushort)0; }
		}

		public bool IsReplyToPresent
		{
			get { return (_presenceSWord & ReplyToPresence) != 0; }
			internal set { _presenceSWord |= value ? ReplyToPresence : (ushort)0; }
		}

		public bool IsExpirationPresent
		{
			get { return (_presenceSWord & ExpirationPresence) != 0; }
			internal set { _presenceSWord |= value ? ExpirationPresence : (ushort)0; }
		}

		public bool IsMessageIdPresent
		{
			get { return (_presenceSWord & MessageIdPresence) != 0; }
			internal set { _presenceSWord |= value ? MessageIdPresence : (ushort)0; }
		}

		public bool IsTimestampPresent
		{
			get { return (_presenceSWord & TimestampPresence) != 0; }
			internal set { _presenceSWord |= value ? TimestampPresence : (ushort)0; }
		}

		public bool IsTypePresent
		{
			get { return (_presenceSWord & TypePresence) != 0; }
			internal set { _presenceSWord |= value ? TypePresence : (ushort)0; }
		}

		public bool IsUserIdPresent
		{
			get { return (_presenceSWord & UserIdPresence) != 0; }
			internal set { _presenceSWord |= value ? UserIdPresence : (ushort)0; }
		}

		public bool IsAppIdPresent
		{
			get { return (_presenceSWord & AppIdPresence) != 0; }
			internal set { _presenceSWord |= value ? AppIdPresence : (ushort)0; }
		}

		public bool IsClusterIdPresent
		{
			get { return (_presenceSWord & ClusterIdPresence) != 0; }
			internal set { _presenceSWord |= value ? ClusterIdPresence : (ushort)0; }
		}

		public string ContentType
		{
			get { return _contentType; }
			set
			{
				ThrowIfFrozen();
				IsContentTypePresent = !string.IsNullOrEmpty(value);
				_contentType = value;
			}
		}

		public string ContentEncoding
		{
			get { return _contentEncoding; }
			set
			{
				ThrowIfFrozen();
				IsContentEncodingPresent = !string.IsNullOrEmpty(value);
				_contentEncoding = value;
			}
		}

		public string CorrelationId
		{
			get { return _correlationId; }
			set
			{
				ThrowIfFrozen();
				IsCorrelationIdPresent = !string.IsNullOrEmpty(value);
				_correlationId = value;
			}
		}

		public string ReplyTo
		{
			get { return _replyTo; }
			set
			{
				ThrowIfFrozen();
				IsReplyToPresent = !string.IsNullOrEmpty(value);
				_replyTo = value;
			}
		}

		/// <summary>
		/// TTL period in milliseconds - https://www.rabbitmq.com/ttl.html
		/// Why is this a string befuddles me..
		/// </summary>
		public string Expiration
		{
			get { return _expiration; }
			set
			{
				ThrowIfFrozen();
				IsExpirationPresent = !string.IsNullOrEmpty(value);
				_expiration = value;
			}
		}

		public string MessageId
		{
			get { return _messageId; }
			set
			{
				ThrowIfFrozen();
				IsMessageIdPresent = !string.IsNullOrEmpty(value);
				_messageId = value;
			}
		}

		public string Type
		{
			get { return _type; }
			set
			{
				ThrowIfFrozen();
				IsTypePresent = !string.IsNullOrEmpty(value);
				_type = value;
			}
		}

		public string UserId
		{
			get { return _userId; }
			set
			{
				ThrowIfFrozen();
				IsUserIdPresent = !string.IsNullOrEmpty(value);
				_userId = value;
			}
		}

		public string AppId
		{
			get { return _appId; }
			set
			{
				ThrowIfFrozen();
				IsAppIdPresent = !string.IsNullOrEmpty(value);
				_appId = value;
			}
		}

		public string ClusterId
		{
			get { return _clusterId; }
			set
			{
				ThrowIfFrozen();
				IsClusterIdPresent = !string.IsNullOrEmpty(value);
				_clusterId = value;
			}
		}

		public byte DeliveryMode
		{
			get { return _deliveryMode; }
			set
			{
				ThrowIfFrozen();
				IsDeliveryModePresent = value != 0;
				_deliveryMode = value;
			}
		}

		public byte Priority
		{
			get { return _priority; }
			set
			{
				ThrowIfFrozen();
				IsPriorityPresent = value != 0;
				_priority = value;
			}
		}

		public AmqpTimestamp? Timestamp
		{
			get { return _timestamp; }
			set
			{
				ThrowIfFrozen();
				IsTimestampPresent = value.HasValue;
				_timestamp = value;
			}
		}

		public IDictionary<string, object> Headers
		{
			get { return _headers; }
//			private set
//			{
//				ThrowIfFrozen();
//				IsHeadersPresent = value != null;
//				_headers = value;
//			}
		}

		#region Implementation of ICloneable

		object ICloneable.Clone()
		{
			var cloned = new BasicProperties(false, false)
			{
				_presenceSWord = this._presenceSWord,
				_timestamp = this._timestamp,
				_deliveryMode = this._deliveryMode,
				_priority = this._priority,
				_contentType = this._contentType,
				_contentEncoding = this._contentEncoding,
				_correlationId = this._correlationId,
				_replyTo = this._replyTo,
				_expiration = this._expiration,
				_messageId = this._messageId,
				_type = this._type,
				_userId = this._userId,
				_appId = this._appId,
				_clusterId = this._clusterId,
			};
			if (this._headers != null && this._headers.Count != 0)
			{
				cloned._headers = new Dictionary<string, object>(this._headers);
			}
			return cloned;
		}

		#endregion

		public BasicProperties Clone()
		{
			return (this as ICloneable).Clone() as BasicProperties;
		}

		internal bool IsFrozen
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (_options & FrozenMask) != 0; }
		}
		internal bool IsReusable
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (_options & ReusableMask) != 0; }
		}

		internal bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if (IsFrozen) return true;
				return _presenceSWord == 0;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfFrozen()
		{
			if (IsFrozen) throw new Exception("This object is frozen so it cannot be changed");
		}

		void IDisposable.Dispose()
		{
			_presenceSWord = 0; // effectilvely reset it
			if (this.Headers != null)
			{
				this.Headers.Clear();
			}
			_deliveryMode = _priority = 0;
			_contentType = _contentEncoding = _correlationId = null;
			_replyTo = _expiration = _messageId = _type = null;
			_userId = _appId = _clusterId = null;
			_timestamp = null;
		}

		internal void Prepare()
		{
			IsHeadersPresent = _headers.Count != 0;
		}
	}
}