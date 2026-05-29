using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EverythingFastAlias.Config
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnPropertyChanged(e);
        }

        public void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null) return;

            _suppressNotification = true;
            try
            {
                Clear();
                foreach (var item in collection)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                // 개수 및 아이템 정보 프로퍼티 변경 통지
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                // 단 한 번의 Reset 알림 전송으로 UI 업데이트
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
