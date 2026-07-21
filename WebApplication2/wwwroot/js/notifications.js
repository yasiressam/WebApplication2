// notifications.js - ملف منفصل لإدارة الإشعارات (بدون jQuery)
(function () {
    // استخدام DOMContentLoaded بدلاً من jQuery
    document.addEventListener('DOMContentLoaded', function () {
        console.log("✅ notifications.js loaded (بدون jQuery)");

        // تحميل الإشعارات عند فتح الصفحة
        loadNotifications();

        // تحميل الإشعارات عند النقر على الجرس (باستخدام JavaScript العادي)
        const notificationsDropdown = document.getElementById('notificationsDropdown');
        if (notificationsDropdown) {
            notificationsDropdown.addEventListener('click', function (e) {
                e.preventDefault();
                console.log("📋 تم النقر على الجرس - جاري تحميل الإشعارات...");
                loadNotifications();
            });
        }

        // تحديث كل 10 ثواني
        setInterval(loadNotifications, 10000);

        // طلب إذن الإشعارات
        if (Notification.permission === "default") {
            Notification.requestPermission();
        }

        // الاتصال بـ SignalR
        connectToSignalR();
    });

    // دالة الاتصال بـ SignalR
    function connectToSignalR() {
        try {
            if (typeof signalR === 'undefined') {
                console.log("⚠️ SignalR غير متوفر");
                return;
            }

            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/notificationHub")
                .withAutomaticReconnect()
                .build();

            connection.start().then(() => {
                console.log("✅ متصل بـ SignalR");
            }).catch(err => {
                console.log("⚠️ SignalR غير متصل:", err);
            });

            connection.on("ReceiveNotification", (notification) => {
                console.log("📢 إشعار جديد:", notification);
                playNotificationSound('/sounds/notification.mp3');
                loadNotifications();

                if (Notification.permission === "granted") {
                    new Notification(notification.title, {
                        body: notification.message,
                        icon: '/images/logo.png'
                    });
                }

                // تأثير وميض للجرس (باستخدام JavaScript العادي)
                const bellIcon = document.querySelector('#notificationsDropdown i');
                if (bellIcon) {
                    bellIcon.classList.add('text-warning');
                    setTimeout(() => bellIcon.classList.remove('text-warning'), 2000);
                }
            });
        } catch (e) {
            console.log("⚠️ خطأ في SignalR:", e);
        }
    }

    // دالة تحميل الإشعارات (باستخدام fetch بدلاً من jQuery)
    function loadNotifications() {
        fetch('/api/notifications/get', {
            method: 'GET',
            cache: 'no-cache',
            headers: {
                'Content-Type': 'application/json'
            }
        })
            .then(response => response.json())
            .then(data => {
                console.log("✅ تم تحميل الإشعارات:", data);
                updateNotificationsDropdown(data);
                updateNotificationsCount(data.unreadCount);
            })
            .catch(error => {
                console.log('❌ فشل تحميل الإشعارات:', error);
            });
    }

    // دالة تحديث قائمة الإشعارات (باستخدام JavaScript العادي)
    function updateNotificationsDropdown(data) {
        const list = document.getElementById('notificationsList');
        if (!list) return;

        if (!data || !data.notifications || data.notifications.length === 0) {
            list.innerHTML = '<div class="text-center text-muted py-4"><i class="bi bi-bell-slash fs-3 d-block mb-2"></i><p class="mb-0 small">لا توجد إشعارات جديدة</p></div>';
            return;
        }

        let html = '';
        data.notifications.forEach(function (notif) {
            const unreadClass = notif.read ? '' : 'unread';
            const timeText = notif.time || 'الآن';

            html += `
                <div class="notification-item ${unreadClass}" onclick="handleNotificationClick(${notif.id})" data-url="${notif.clickUrl || ''}">
                    <div class="d-flex align-items-start">
                        <div class="notification-icon me-2">
                            <i class="bi ${escapeHtml(notif.icon) || 'bi-bell'}"></i>
                        </div>
                        <div class="flex-grow-1">
                            <div class="d-flex justify-content-between align-items-center">
                                <strong class="small">${escapeHtml(notif.title)}</strong>
                                <small class="notification-time">${timeText}</small>
                            </div>
                            <p class="mb-0 small text-muted">${escapeHtml(notif.message)}</p>
                        </div>
                    </div>
                </div>
            `;
        });

        list.innerHTML = html;
    }

    // دالة تحديث عداد الإشعارات
    function updateNotificationsCount(count) {
        const counter = document.getElementById('notificationsCount');
        if (!counter) return;

        if (count && count > 0) {
            counter.textContent = count;
            counter.style.display = '';
        } else {
            counter.style.display = 'none';
        }
    }

    // دالة النقر على الإشعار
    window.handleNotificationClick = async function (notificationId) {
        if (!notificationId) return;

        const element = document.querySelector(`[onclick="handleNotificationClick(${notificationId})"]`);
        const destinationUrl = element?.dataset.url || `/Notifications/Details/${notificationId}`;

        // إرسال طلب لتحديد الإشعار كمقروء
        try {
            const response = await fetch('/api/notifications/mark-read', {
                method: 'POST',
                keepalive: true,
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getCsrfToken()
                },
                body: JSON.stringify(notificationId)
            });

            if (response.ok) {
                const counter = document.getElementById('notificationsCount');
                if (counter) {
                    const currentCount = parseInt(counter.textContent) || 0;
                    if (currentCount > 0) {
                        counter.textContent = currentCount - 1;
                        if (currentCount - 1 === 0) counter.style.display = 'none';
                    }
                }
                if (element) element.classList.remove('unread');
            }
        } catch (error) {
            console.log('❌ فشل تحديث الإشعار:', error);
        }

        // فتح صفحة التفاصيل
        window.location.assign(destinationUrl);
    };

    // دالة تحديد الكل كمقروء
    window.markAllAsRead = function () {
        fetch('/api/notifications/mark-all-read', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getCsrfToken()
            }
        })
            .then(response => response.json())
            .then(() => {
                document.querySelectorAll('#notificationsList .notification-item.unread').forEach(el => {
                    el.classList.remove('unread');
                });
                const counter = document.getElementById('notificationsCount');
                if (counter) counter.style.display = 'none';
                loadNotifications();

                // تأثير نجاح
                const bellIcon = document.querySelector('#notificationsDropdown i');
                if (bellIcon) {
                    bellIcon.classList.add('text-success');
                    setTimeout(() => bellIcon.classList.remove('text-success'), 1000);
                }
            })
            .catch(error => {
                console.log('❌ فشل تحديث الكل:', error);
            });
    };

    // دالة الحصول على CSRF Token
    function getCsrfToken() {
        return window.getCsrfToken ? window.getCsrfToken() :
            document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    // دالة الهروب من HTML
    function escapeHtml(unsafe) {
        if (!unsafe) return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // دالة تشغيل الصوت
    function playNotificationSound(soundPath) {
        try {
            const audio = new Audio(soundPath);
            audio.volume = 0.5;
            audio.play().catch(() => {
                console.log("⚠️ فشل تشغيل الصوت");
            });
        } catch (e) {
            console.log("⚠️ خطأ في تشغيل الصوت");
        }
    }
})();
