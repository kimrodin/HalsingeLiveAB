import { useEffect, useMemo, useRef, useState } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { eventPageSize, getEventPage } from '../api/events'
import { getVenues } from '../../venues/api/venues'
import { useNow } from '../../../shared/hooks/useNow'
import EventCarousel from '../components/EventCarousel'
import EventCard from '../components/EventCard'
import { getSaleState } from '../components/saleState'
import bannerImage from '../../../assets/banner.png'

type SaleFilter = 'all' | 'onSale' | 'upcoming'

const saleFilters: Array<{ value: SaleFilter; label: string }> = [
  { value: 'all', label: 'Alla' },
  { value: 'onSale', label: 'Biljetter ute' },
  { value: 'upcoming', label: 'Kommande släpp' },
]

export default function HomePage() {
  const now = useNow()
  const [searchTerm, setSearchTerm] = useState('')
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState('')
  const [saleFilter, setSaleFilter] = useState<SaleFilter>('all')
  const loadMoreRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearchTerm(searchTerm.trim()), 300)
    return () => window.clearTimeout(timeout)
  }, [searchTerm])

  const eventsQuery = useInfiniteQuery({
    queryKey: ['events', 'catalog', debouncedSearchTerm, Math.floor(now / 60_000)],
    queryFn: ({ pageParam, signal }) => getEventPage(
      pageParam,
      { eventName: debouncedSearchTerm || undefined, endsAfter: new Date(now).toISOString() },
      signal,
    ),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
  })
  const venuesQuery = useQuery({
    queryKey: ['venues'],
    queryFn: ({ signal }) => getVenues(signal),
  })
  const {
    hasNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
    fetchNextPage,
  } = eventsQuery

  const venuesById = useMemo(
    () => new Map(venuesQuery.data?.map((venue) => [venue.id, `${venue.name}, ${venue.city}`]) ?? []),
    [venuesQuery.data],
  )

  const events = eventsQuery.data?.pages.flatMap((page) => page.events) ?? []
  const visibleEvents = events.filter((event) => new Date(event.endDate).getTime() >= now)
  const filteredEvents = visibleEvents.filter((event) =>
    saleFilter === 'all' || getSaleState(event, now) === saleFilter,
  )
  const featuredEvents = visibleEvents.slice(0, 3)
  const totalCount = eventsQuery.data?.pages[0]?.totalCount ?? 0

  useEffect(() => {
    const target = loadMoreRef.current
    if (!target || !hasNextPage || isFetchingNextPage || isFetchNextPageError || filteredEvents.length === 0) return

    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void fetchNextPage()
    }, { rootMargin: '300px' })
    observer.observe(target)
    return () => observer.disconnect()
  }, [hasNextPage, isFetchingNextPage, isFetchNextPageError, filteredEvents.length, fetchNextPage])

  return (
    <Stack spacing={{ xs: 1.5, sm: 3 }} useFlexGap>
      <Stack
        spacing={{ xs: 1.5, sm: 3 }}
        sx={{
          position: 'relative',
          isolation: 'isolate',
          mt: { xs: -3, sm: -5 },
          pt: { xs: 2, sm: 5 },
          pb: { xs: 2, sm: 3 },
          '&::before': {
            content: '""',
            position: 'absolute',
            zIndex: -1,
            top: 'calc(-1 * var(--customer-header-height, 77px))',
            bottom: 0,
            left: 'calc(50% - 50vw)',
            width: '100vw',
            backgroundColor: '#062b3c',
            backgroundImage: `url("${bannerImage}")`,
            backgroundSize: 'cover',
            backgroundPosition: 'center bottom',
            backgroundRepeat: 'no-repeat',
          },
        }}
      >
        <Box sx={{ display: { xs: 'none', sm: 'block' } }}>
          <EventCarousel events={featuredEvents} venuesById={venuesById} now={now} />
        </Box>

        <Stack spacing={0.25} sx={{ display: { xs: 'flex', sm: 'none' } }}>
          <Typography variant="h3" component="h1" sx={{ color: 'common.white', fontSize: '2.25rem', lineHeight: 1.1 }}>
            Evenemang
          </Typography>
          <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.82)' }}>
            Hitta nästa upplevelse
          </Typography>
        </Stack>

        <TextField
          id="event-search"
          label="Sök evenemang"
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
          fullWidth
          sx={{
            bgcolor: 'background.paper',
            borderRadius: 1,
            '& .MuiOutlinedInput-root': { minHeight: { xs: 48, sm: 56 } },
          }}
        />

        <Stack
          direction="row"
          role="group"
          aria-label="Filtrera evenemang efter biljettstatus"
          sx={{
            display: { xs: 'flex', sm: 'none' },
            width: '100%',
            minWidth: 0,
            overflowX: 'auto',
            gap: 1,
            pb: 0.5,
            scrollbarWidth: 'none',
            '&::-webkit-scrollbar': { display: 'none' },
          }}
        >
          {saleFilters.map((filter) => {
            const selected = saleFilter === filter.value
            return (
              <Chip
                key={filter.value}
                label={filter.label}
                clickable
                aria-pressed={selected}
                onClick={() => setSaleFilter(filter.value)}
                sx={{
                  flexShrink: 0,
                  height: 40,
                  borderRadius: 999,
                  color: selected ? 'primary.dark' : 'common.white',
                  bgcolor: selected ? 'background.paper' : 'rgba(255,255,255,0.12)',
                  border: '1px solid',
                  borderColor: selected ? 'background.paper' : 'rgba(255,255,255,0.32)',
                  '&:hover': { bgcolor: selected ? 'background.paper' : 'rgba(255,255,255,0.2)' },
                }}
              />
            )
          })}
        </Stack>
      </Stack>

      {eventsQuery.isPending ? (
        <Stack spacing={2} role="status" aria-busy="true" aria-label="Hämtar evenemang">
          {[1, 2, 3].map((item) => <Skeleton key={item} variant="rounded" height={150} />)}
        </Stack>
      ) : eventsQuery.isError && !eventsQuery.data ? (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => void eventsQuery.refetch()}>Försök igen</Button>}
        >
          Evenemangen kunde inte hämtas just nu. Kontrollera anslutningen och försök igen.
        </Alert>
      ) : visibleEvents.length === 0 ? (
        <Alert severity="info">Inga evenemang hittades.</Alert>
      ) : filteredEvents.length === 0 ? (
        <Alert
          severity={isFetchNextPageError ? 'error' : 'info'}
          action={hasNextPage && (
            <Button
              color="inherit"
              size="small"
              onClick={() => void fetchNextPage()}
              disabled={isFetchingNextPage}
            >
              {isFetchingNextPage ? 'Hämtar…' : 'Visa fler'}
            </Button>
          )}
        >
          {isFetchNextPageError
            ? 'Fler evenemang kunde inte hämtas.'
            : 'Inga evenemang matchar det här filtret.'}
        </Alert>
      ) : (
        <>
          <Stack
            direction="row"
            role="status"
            sx={{
              display: { xs: 'flex', sm: 'none' },
              alignItems: 'baseline',
              justifyContent: 'space-between',
            }}
          >
            <Typography variant="h5" component="h2" sx={{ fontWeight: 700 }}>
              Kommande
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {filteredEvents.length} evenemang
            </Typography>
          </Stack>
          <Typography
            variant="body2"
            color="text.secondary"
            role="status"
            sx={{ display: { xs: 'none', sm: 'block' } }}
          >
            Visar {filteredEvents.length} av {totalCount} evenemang
          </Typography>
          <Box
            id="event-list"
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'minmax(0, 1fr)' },
              gap: { xs: 1.5, sm: 2 },
            }}
          >
            {filteredEvents.map((event) => (
              <EventCard key={event.eventId} event={event} venueName={venuesById.get(event.venueId)} now={now} />
            ))}
          </Box>
          {eventsQuery.isFetchNextPageError && (
            <Alert
              severity="error"
              action={<Button color="inherit" size="small" onClick={() => void eventsQuery.fetchNextPage()}>Försök igen</Button>}
            >
              Fler evenemang kunde inte hämtas.
            </Alert>
          )}
          {eventsQuery.hasNextPage && (
            <Stack ref={loadMoreRef} spacing={1} sx={{ alignItems: 'center' }} aria-live="polite">
              <Button
                variant="outlined"
                onClick={() => void eventsQuery.fetchNextPage()}
                disabled={eventsQuery.isFetchingNextPage || eventsQuery.isFetchNextPageError}
              >
                {eventsQuery.isFetchingNextPage ? 'Hämtar fler evenemang…' : `Visa fler (${eventPageSize})`}
              </Button>
            </Stack>
          )}
        </>
      )}
    </Stack>
  )
}
