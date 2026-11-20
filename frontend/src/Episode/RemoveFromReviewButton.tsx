import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

interface RemoveFromReviewButtonProps {
  reviewItemId: number;
}

function RemoveFromReviewButton({ reviewItemId }: RemoveFromReviewButtonProps) {
  const queryClient = useQueryClient();
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const { mutate, isPending } = useApiMutation<unknown, void>({
    path: `/review/${reviewItemId}`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: () => {
        setIsConfirmOpen(false);
        queryClient.invalidateQueries({ queryKey: ['/review'] });
        queryClient.invalidateQueries({ queryKey: ['/episode'] });
        queryClient.invalidateQueries({ queryKey: ['/calendar'] });
        queryClient.invalidateQueries({ queryKey: ['/wanted/missing'] });
        queryClient.invalidateQueries({ queryKey: ['/wanted/cutoff'] });
      },
    },
  });

  const handlePress = useCallback(() => {
    setIsConfirmOpen(true);
  }, []);

  const handleConfirm = useCallback(() => {
    mutate();
  }, [mutate]);

  const handleCancel = useCallback(() => {
    setIsConfirmOpen(false);
  }, []);

  return (
    <>
      <Button kind={kinds.DANGER} isDisabled={isPending} onPress={handlePress}>
        {translate('RemoveFromReview')}
      </Button>

      <ConfirmModal
        isOpen={isConfirmOpen}
        kind={kinds.DANGER}
        title={translate('RemoveFromReview')}
        message={translate('RemoveFromReviewMessage')}
        confirmLabel={translate('RemoveFromReview')}
        isSpinning={isPending}
        onConfirm={handleConfirm}
        onCancel={handleCancel}
      />
    </>
  );
}

export default RemoveFromReviewButton;
